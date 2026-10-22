using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Drives the player's PartRig from PlayerMotor state (run/idle/air/slide/land + weapon swing). R3 hooks, all off by
    /// default: the weapon can mirror its swing and spin the whole model (the Labyrinth Chain's backswings and whirl), and
    /// a trait can swell the model, lean the body and drop the head (Asterion's Rage).
    /// </summary>
    public sealed class PlayerPartAnimator : MonoBehaviour, ITickable
    {
        public PartAnimProfile profile;
        public PlayerMotor motor;
        [Tooltip("The model root under the player (gets the slide drop + squash).")]
        public Transform visualRoot;

        /// <summary>Set by the melee weapon: 0..1 through the swing, negative = not swinging.</summary>
        public float AttackT { get; set; } = -1f;
        /// <summary>World direction of the current swing (the body twists toward it).</summary>
        public Vector3 AttackDirection { get; set; } = Vector3.forward;
        /// <summary>Collapse into the death pose (placeholder until M3.3's proper death animation).</summary>
        public bool Dead { get; set; }
        /// <summary>The swing goes the other way: the arm's yaw and the body's twist along with it are negated (a sweep
        /// from left to right). Set by the weapon.</summary>
        public bool AttackMirror { get; set; }
        /// <summary>Degrees the whole model turns about Y on top of the rig (a whirl). Set by the weapon; 0 = its bind rotation.</summary>
        public float AttackSpin { get; set; }
        /// <summary>Multiplies the model's scale, eased by the pose blend (Asterion swells while Unchained). Set by a trait.</summary>
        public float ScaleBoost { get; set; } = 1f;
        /// <summary>Forward body lean added (deg). Set by a trait.</summary>
        public float ExtraLean { get; set; }
        /// <summary>The head pitched down (deg); it also cancels the idle head lift. Set by a trait.</summary>
        public float HeadDrop { get; set; }

        PartRig _rig;
        PartPose _pose = PartPose.Identity;
        float _phase, _time, _stillT, _scaleBoost = 1f;
        Quaternion _rootBind = Quaternion.identity;
        bool _spun;

        void Awake()
        {
            _rig = new PartRig(visualRoot);
            if (visualRoot != null) _rootBind = visualRoot.localRotation;
        }
        void OnEnable() => GameLoop.Register(this, TickGroup.Feedback);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            if (!_rig.IsValid || dt <= 0f) return;
            var p = profile;
            _time += dt;
            float speed = motor.HorizontalSpeed;
            float run01 = Mathf.Clamp01(speed / p.fullRunSpeed);
            var target = PartPose.Identity;
            bool still = false;

            if (Dead)
            {
                // knees give, body tips back, sword arm drops: a readable "down" at 64 px
                target.Body = PartPose.Lean(-70f);
                target.Head = PartPose.Lean(-25f);
                target.LegL = PartPose.Swing(85f);
                target.LegR = PartPose.Swing(70f);
                target.ArmL = PartPose.Swing(-35f) + new Vector3(0, 0, -50f);
                target.ArmR = PartPose.Swing(-20f) + new Vector3(0, 0, 55f);
                target.RootY = -0.55f;
            }
            else switch (motor.CurrentState)
            {
                case PlayerMotor.State.Sliding:
                    target.Body = PartPose.Lean(p.slideBodyLean);
                    target.Head = PartPose.Lean(-p.slideBodyLean * 0.6f);
                    target.LegL = PartPose.Swing(p.slideLegForward);
                    target.LegR = PartPose.Swing(p.slideLegForward * 0.55f);
                    target.ArmL = PartPose.Swing(-p.slideArmBack) + new Vector3(0, 0, -25f);
                    target.ArmR = PartPose.Swing(-p.slideArmBack * 0.5f) + new Vector3(0, 0, 25f);
                    target.RootY = -p.slideDrop;
                    break;

                case PlayerMotor.State.Airborne:
                    float rising = Mathf.Clamp01(motor.Velocity.y / 8f);
                    target.Body = PartPose.Lean(run01 * p.runLean * 0.5f);
                    target.LegL = PartPose.Swing(p.airLegTuck);
                    target.LegR = PartPose.Swing(-p.airLegTuck * 0.4f);
                    target.ArmL = PartPose.Swing(p.airArmRaise * (0.5f + 0.5f * rising)) + new Vector3(0, 0, -20f);
                    target.ArmR = PartPose.Swing(p.airArmRaise * 0.6f) + new Vector3(0, 0, 20f);
                    break;

                default:
                    if (run01 > 0.05f)
                    {
                        _phase += speed * dt / Mathf.Max(0.1f, p.strideLength) * Mathf.PI * 2f;
                        float s = Mathf.Sin(_phase);
                        target.LegL = PartPose.Swing(s * p.legSwing * run01);
                        target.LegR = PartPose.Swing(-s * p.legSwing * run01);
                        target.ArmL = PartPose.Swing(-s * p.armSwing * run01);
                        target.ArmR = PartPose.Swing(s * p.armSwing * p.weaponArmSwingScale * run01);
                        target.Body = PartPose.Lean(p.runLean * run01) + new Vector3(0, s * 6f * run01, 0);
                        target.Head = PartPose.Lean(-p.runLean * 0.5f * run01);
                        target.RootY = Mathf.Abs(s) * p.bobHeight * run01;
                    }
                    else
                    {
                        float b = Mathf.Sin(_time * p.idleBobHz * Mathf.PI * 2f);
                        target.RootY = b * p.idleBobHeight;
                        target.ArmL = new Vector3(0, 0, -p.idleArmSway * (0.5f + 0.5f * b));
                        target.ArmR = new Vector3(0, 0, p.idleArmSway * (0.5f + 0.5f * b));
                        still = AttackT < 0f;
                    }
                    break;
            }

            // Standing still a while, the head lifts (Asterion looks at the sky); a trait's head drop wins.
            _stillT = still ? _stillT + dt : 0f;
            if (!Dead && p.idleHeadLift != 0f && HeadDrop <= 0f && _stillT >= p.idleHeadDelay)
                target.Head += PartPose.Lean(-p.idleHeadLift);
            if (!Dead)
            {
                target.Body += PartPose.Lean(ExtraLean);
                target.Head += PartPose.Lean(HeadDrop);
            }

            // Weapon swing overrides the sword arm (additive body twist sells it).
            if (AttackT >= 0f && !Dead)
            {
                float k = p.swingCurve.Evaluate(Mathf.Clamp01(AttackT));
                float yaw = Mathf.Lerp(p.swingYawStart, p.swingYawEnd, k);
                if (AttackMirror) yaw = -yaw; // the backswing: the same swing the other way
                float raise = Mathf.Sin(k * Mathf.PI) * 0.4f + 0.6f;
                target.ArmR = new Vector3(-p.swingRaise * raise, yaw, 0f);
                if (p.overheadChop)
                {
                    yaw = 0f;
                    target.ArmR = new Vector3(-Mathf.Lerp(p.chopPitchStart, p.chopPitchEnd, k), 0f, 0f);
                    target.Body += new Vector3(Mathf.Lerp(-8f, 22f, k), 0f, 0f); // leans back, then bends into the chop
                }
                if (p.twoHanded) target.ArmL = new Vector3(target.ArmR.x, -target.ArmR.y, -target.ArmR.z);
                // Twist the upper body toward the target (the legs keep running where the player steers).
                Vector3 fwd = transform.forward;
                float twist = Mathf.Clamp(Vector3.SignedAngle(new Vector3(fwd.x, 0f, fwd.z), AttackDirection, Vector3.up), -80f, 80f);
                target.Body += new Vector3(0f, twist + yaw * p.bodyTwistScale, 0f);
            }

            float blend = 1f - Mathf.Exp(-p.poseSharpness * dt);
            _pose.LerpTo(target, blend);

            // Squash & stretch (not smoothed: it's the snap that sells it).
            var scale = Vector3.one;
            if (motor.TimeSinceLanded < p.landSquashTime)
            {
                float k = 1f - motor.TimeSinceLanded / p.landSquashTime;
                float sq = p.landSquash * k * k;
                scale = new Vector3(1f + sq * 0.5f, 1f - sq, 1f + sq * 0.5f);
            }
            else if (motor.TimeSinceJump < p.jumpStretchTime)
            {
                float k = 1f - motor.TimeSinceJump / p.jumpStretchTime;
                float st = p.jumpStretch * k;
                scale = new Vector3(1f - st * 0.4f, 1f + st, 1f - st * 0.4f);
            }
            _scaleBoost = Mathf.LerpUnclamped(_scaleBoost, ScaleBoost, blend);
            var applied = _pose;
            applied.RootScale = scale * _scaleBoost;
            _rig.Apply(applied);

            // A whirl turns the whole model about Y (the rig doesn't touch the root's rotation); back to bind at 0.
            if (AttackSpin != 0f) { visualRoot.localRotation = _rootBind * Quaternion.Euler(0f, AttackSpin, 0f); _spun = true; }
            else if (_spun) { visualRoot.localRotation = _rootBind; _spun = false; }
        }
    }
}
