using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Physics layers (names are written to the TagManager by the project setup script).</summary>
    public static class Layers
    {
        public const int Environment = 8;
        public const int Player = 9;
        public const int Enemy = 10;
        public const int Pickup = 11;
        public const int Projectile = 12;
        public const int Trigger = 13;
        /// <summary>Small static props (crates, barrels, fences): solid for the player, ignored by the camera.</summary>
        public const int Props = 14;

        /// <summary>Big static geometry: walkable, and the camera stays out of it.</summary>
        public const int EnvironmentMask = 1 << Environment;
        /// <summary>Everything the player can stand on / collide with.</summary>
        public const int SolidMask = (1 << Environment) | (1 << Props);
    }

    /// <summary>Observed numbers, so feel can be verified without playing (M1.1 acceptance).</summary>
    [System.Serializable]
    public struct MotorTelemetry
    {
        public float TopHorizontalSpeed;
        public float TopSlideSpeed;
        public float LastJumpHeight;
        public float LastSlideBoost;
        public float LastLandSpeed;
        public int Jumps, Slides, Lands;
    }

    /// <summary>
    /// Kinematic third-person controller on a CharacterController (CLAUDE.md §4): accel/decel, gravity, coyote time,
    /// jump buffer, variable jump, slide (boost, downhill gain, steering, momentum), air control that keeps momentum,
    /// optional air jumps. All numbers live in <see cref="MovementConfig"/>. Ticked centrally (TickGroup.Player).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Player))]
    public sealed class PlayerMotor : MonoBehaviour, ITickable
    {
        public enum State { Grounded, Airborne, Sliding }

        public MovementConfig config;

        public State CurrentState { get; private set; } = State.Airborne;
        public Vector3 Velocity => new Vector3(_hv.x, _vy, _hv.z);
        public Vector3 HorizontalVelocity => _hv;
        public float HorizontalSpeed => _hv.magnitude;
        public bool IsGrounded => CurrentState != State.Airborne;
        public bool IsSliding => CurrentState == State.Sliding;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        /// <summary>Seconds since the last landing (for squash), large when airborne.</summary>
        public float TimeSinceLanded { get; private set; } = 10f;
        public float TimeSinceJump { get; private set; } = 10f;
        /// <summary>Air jumps granted on top of config.extraJumps (upgrades).</summary>
        public int BonusAirJumps { get; set; }
        /// <summary>Run/air speed multiplier (upgrades). Slide physics are left alone on purpose.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public MotorTelemetry Telemetry;

        CharacterController _cc;
        Player _player;
        Vector3 _hv;          // horizontal velocity
        float _vy;            // vertical velocity
        float _vyStep;        // vertical velocity used for this frame's displacement (average over the step)
        float _coyote, _jumpBuffer, _slideCooldown;
        int _airJumpsUsed;
        bool _jumpCutDone, _ascendingFromJump;
        float _takeoffY, _apexY;
        Vector3 _pendingImpulse;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _player = GetComponent<Player>();
            ApplyCapsule(standing: true);
            _cc.stepOffset = config.stepOffset;
            _cc.slopeLimit = config.slopeLimit;
            _cc.skinWidth = 0.04f;
            _cc.minMoveDistance = 0f;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Player);
        void OnDisable() => GameLoop.Unregister(this);

        public void Teleport(Vector3 position)
        {
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
            _hv = Vector3.zero;
            _vy = 0f;
            _pendingImpulse = Vector3.zero;
        }

        /// <summary>
        /// Shove the player (velocity change, m/s), applied at the start of the next motor tick. A positive y launches
        /// them into the air (no jump cut, counts as airborne) — e.g. the Brute's slam.
        /// </summary>
        public void AddImpulse(Vector3 velocity) => _pendingImpulse += velocity;

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            var c = config;
            var input = _player.Input;

            // --- intent: stick relative to the camera yaw
            Vector3 wish = Quaternion.Euler(0f, _player.ReferenceYaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            float wishMag = Mathf.Clamp01(wish.magnitude);
            Vector3 wishDir = wishMag > 0.01f ? wish / wish.magnitude : Vector3.zero;

            if (input.JumpPressed) _jumpBuffer = c.jumpBuffer; else _jumpBuffer -= dt;
            _slideCooldown -= dt;
            TimeSinceLanded += dt;
            TimeSinceJump += dt;

            // --- ground probe
            bool wasGrounded = IsGrounded;
            bool grounded = ProbeGround(out var groundNormal);
            GroundNormal = grounded ? groundNormal : Vector3.up;

            if (grounded && !wasGrounded) Land(input);
            else if (!grounded && wasGrounded)
            {
                if (CurrentState == State.Sliding) ApplyCapsule(standing: true);
                CurrentState = State.Airborne;
                _coyote = _ascendingFromJump ? 0f : c.coyoteTime;
                _takeoffY = _apexY = transform.position.y;
            }
            if (!grounded) _coyote -= dt;

            // --- external impulse (knock-up / shove)
            if (_pendingImpulse != Vector3.zero)
            {
                _hv += new Vector3(_pendingImpulse.x, 0f, _pendingImpulse.z);
                if (_pendingImpulse.y > 0f)
                {
                    if (CurrentState == State.Sliding) ApplyCapsule(standing: true);
                    CurrentState = State.Airborne;
                    _vy = Mathf.Max(_vy, _pendingImpulse.y);
                    _ascendingFromJump = true; // keeps the ground probe from snapping back down
                    _jumpCutDone = true;
                    _coyote = 0f;
                    _takeoffY = _apexY = transform.position.y;
                }
                _pendingImpulse = Vector3.zero;
            }

            // --- jump (ground / coyote / air)
            if (_jumpBuffer > 0f)
            {
                if (IsGrounded || _coyote > 0f) Jump(airJump: false);
                else if (_airJumpsUsed < c.extraJumps + BonusAirJumps) { _airJumpsUsed++; Jump(airJump: true); }
            }

            // --- state update
            _vyStep = _vy;
            switch (CurrentState)
            {
                case State.Grounded: GroundMove(wishDir, wishMag, input, dt); break;
                case State.Sliding: SlideMove(wishDir, input, dt); break;
                default: AirMove(wishDir, wishMag, input, dt); break;
            }

            // --- integrate
            Vector3 move;
            if (IsGrounded)
            {
                Vector3 along = Vector3.ProjectOnPlane(_hv, GroundNormal);
                float hs = _hv.magnitude;
                if (along.sqrMagnitude > 1e-8f) along = along.normalized * hs;
                move = along + Vector3.down * c.groundStick;
                _vy = 0f;
            }
            else move = _hv + Vector3.up * _vyStep;

            Vector3 before = transform.position;
            var flags = _cc.Move(move * dt);
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0f) _vy = 0f;
            if ((flags & CollisionFlags.Sides) != 0 && dt > 0f)
            {
                // Remove velocity into walls so it doesn't build up while pressing against them. The part of the
                // intended horizontal motion the controller didn't make is the into-the-wall part (no
                // OnControllerColliderHit: Unity allocates a hit object per callback).
                Vector3 moved = (transform.position - before) / dt;
                Vector3 blocked = new Vector3(move.x - moved.x, 0f, move.z - moved.z);
                if (blocked.sqrMagnitude > 1e-4f && TryWallNormal(blocked.normalized, out var n))
                {
                    float into = Vector3.Dot(_hv, n);
                    if (into < 0f) _hv -= n * into;
                }
            }

            if (!IsGrounded && transform.position.y > _apexY) _apexY = transform.position.y;

            // --- facing
            Vector3 faceDir = _hv.sqrMagnitude > 0.25f ? _hv : (wishMag > 0.1f ? wishDir : Vector3.zero);
            if (faceDir.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir), c.faceTurnSpeed * dt);

            float hsNow = _hv.magnitude;
            if (hsNow > Telemetry.TopHorizontalSpeed) Telemetry.TopHorizontalSpeed = hsNow;
            if (IsSliding && hsNow > Telemetry.TopSlideSpeed) Telemetry.TopSlideSpeed = hsNow;
        }

        /// <summary>
        /// Real contact normal toward `dir` via a short capsule cast (allocation-free). Only near-vertical surfaces
        /// count as walls: ledge corners and slopes (normal.y > 0.3) must not eat momentum, or the player sticks at edges.
        /// </summary>
        bool TryWallNormal(Vector3 dir, out Vector3 normal)
        {
            normal = default;
            float r = _cc.radius * 0.95f;
            Vector3 center = transform.position + _cc.center;
            float half = Mathf.Max(0f, _cc.height * 0.5f - _cc.radius);
            Vector3 top = center + Vector3.up * half, bottom = center - Vector3.up * half;
            if (!Physics.CapsuleCast(top, bottom, r, dir, out var hit, _cc.skinWidth + 0.1f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y > 0.3f) return false;
            Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (n.sqrMagnitude < 1e-4f) return false;
            normal = n.normalized;
            return true;
        }

        void GroundMove(Vector3 wishDir, float wishMag, in PlayerInputFrame input, float dt)
        {
            var c = config;
            float speed = _hv.magnitude;

            bool wantsSlide = input.SlidePressed || (input.SlideHeld && !c.slideRequiresHold);
            if (wantsSlide && _slideCooldown <= 0f && speed >= c.slideMinStartSpeed)
            {
                StartSlide(boost: c.slideStartBoost, wishDir);
                return;
            }

            float maxSpeed = c.maxSpeed * SpeedMultiplier * DevSettings.MoveSpeedMul;
            if (speed > maxSpeed + 0.01f && wishMag > 0.01f)
            {
                // Above run speed (after a slide / slide-jump): keep momentum, steer, bleed off slowly.
                Vector3 dir = Vector3.RotateTowards(_hv / speed, wishDir, c.highSpeedTurnRate * Mathf.Deg2Rad * dt, 0f);
                speed = Mathf.MoveTowards(speed, maxSpeed, c.overspeedDecel * dt);
                _hv = dir * speed;
            }
            else
            {
                Vector3 target = wishDir * (maxSpeed * wishMag);
                float accel = wishMag > 0.01f ? c.groundAccel : (speed > maxSpeed ? c.overspeedDecel : c.groundDecel);
                _hv = Vector3.MoveTowards(_hv, target, accel * dt);
            }
        }

        void StartSlide(float boost, Vector3 wishDir)
        {
            var c = config;
            float before = _hv.magnitude;
            Vector3 dir = before > 0.1f ? _hv / before : (wishDir.sqrMagnitude > 0f ? wishDir : transform.forward);
            float after = Mathf.Min(before + boost, c.slideMaxSpeed);
            _hv = dir * after;
            CurrentState = State.Sliding;
            ApplyCapsule(standing: false);
            Telemetry.LastSlideBoost = after - before;
            Telemetry.Slides++;
            GameEvents.OnSlide.Raise(new SlideEvent { Position = transform.position, Velocity = _hv, Boost = after - before });
        }

        void SlideMove(Vector3 wishDir, in PlayerInputFrame input, float dt)
        {
            var c = config;
            // Downhill gravity component accelerates the slide (uphill slows it).
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
            Vector3 slopeAccel = new Vector3(downhill.x, 0f, downhill.z) * (c.gravity * c.slopeGain);
            _hv += slopeAccel * dt;

            float speed = _hv.magnitude;
            speed = Mathf.MoveTowards(speed, 0f, c.slideFriction * dt);
            Vector3 dir = speed > 1e-3f ? _hv.normalized : transform.forward;
            if (wishDir.sqrMagnitude > 0f)
                dir = Vector3.RotateTowards(dir, wishDir, c.slideSteerRate * Mathf.Deg2Rad * dt, 0f);
            _hv = dir * Mathf.Min(speed, c.slideMaxSpeed);

            bool goingDownhill = Vector3.Dot(slopeAccel, dir) > 0.5f;
            bool released = c.slideRequiresHold && !input.SlideHeld;
            if ((released || (speed < c.slideMinSpeed && !goingDownhill)) && HasHeadroom())
            {
                CurrentState = State.Grounded;
                ApplyCapsule(standing: true);
                _slideCooldown = c.slideCooldown;
            }
        }

        void AirMove(Vector3 wishDir, float wishMag, in PlayerInputFrame input, float dt)
        {
            var c = config;
            float vyStart = _vy;
            _vy -= c.gravity * (_vy < 0f ? c.fallGravityMultiplier : 1f) * dt;
            if (_vy < -c.maxFallSpeed) _vy = -c.maxFallSpeed;

            if (_ascendingFromJump && !_jumpCutDone && _vy > 0f && !input.JumpHeld)
            {
                _vy *= c.jumpCutMultiplier;
                _jumpCutDone = true;
            }
            if (_vy <= 0f) _ascendingFromJump = false;
            // Trapezoid step: exact for constant gravity, so jump height is the same at 60 and 240 fps
            // (plain semi-implicit Euler lost ~v0·dt/2 ≈ 8 cm of a 1.7 m jump at 60 fps).
            _vyStep = (vyStart + _vy) * 0.5f;

            float speed = _hv.magnitude;
            if (wishMag > 0.01f)
            {
                // Quake-style: accelerate along the wish direction up to airMaxSpeed; never brakes existing momentum.
                float along = Vector3.Dot(_hv, wishDir);
                float airMax = c.airMaxSpeed * SpeedMultiplier * DevSettings.MoveSpeedMul;
                float add = airMax * wishMag - along;
                if (add > 0f) _hv += wishDir * Mathf.Min(c.airAccel * dt, add);
                speed = _hv.magnitude;
                if (speed > airMax) _hv = _hv / speed * Mathf.Max(airMax, speed - c.airOverspeedDrag * dt);
            }
            else if (speed > 0f)
            {
                _hv = _hv / speed * Mathf.Max(0f, speed - c.airIdleDrag * dt);
            }
        }

        void Jump(bool airJump)
        {
            var c = config;
            bool fromSlide = CurrentState == State.Sliding;
            if (fromSlide) ApplyCapsule(standing: true);
            float h = airJump ? c.jumpHeight * c.airJumpHeightMultiplier : c.jumpHeight;
            _vy = Mathf.Sqrt(2f * c.gravity * h);
            if (fromSlide && _hv.sqrMagnitude > 0.01f)
                _hv += _hv.normalized * c.slideJumpBoost;
            CurrentState = State.Airborne;
            _coyote = 0f;
            _jumpBuffer = 0f;
            _jumpCutDone = false;
            _ascendingFromJump = true;
            TimeSinceJump = 0f;
            _takeoffY = _apexY = transform.position.y;
            Telemetry.Jumps++;
            GameEvents.OnJump.Raise(new JumpEvent { Position = transform.position, FromSlide = fromSlide, AirJump = airJump });
        }

        void Land(in PlayerInputFrame input)
        {
            var c = config;
            float fallSpeed = -_vy;
            Telemetry.LastLandSpeed = fallSpeed;
            Telemetry.LastJumpHeight = _apexY - _takeoffY;
            Telemetry.Lands++;
            _airJumpsUsed = 0;
            _ascendingFromJump = false;
            TimeSinceLanded = 0f;
            CurrentState = State.Grounded;
            GameEvents.OnLand.Raise(new LandEvent { Position = transform.position, FallSpeed = fallSpeed });
            if (c.slideOnLand && input.SlideHeld && _hv.magnitude >= c.slideMinStartSpeed && _slideCooldown <= 0f)
                StartSlide(boost: 0f, Vector3.zero);
        }

        bool ProbeGround(out Vector3 normal)
        {
            normal = Vector3.up;
            if (_ascendingFromJump && _vy > 0.01f) return false; // just jumped: don't snap back down
            float r = _cc.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.05f);
            float dist = IsGrounded ? 0.25f : 0.1f;
            if (!Physics.SphereCast(origin, r, Vector3.down, out var hit, dist, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > config.slopeLimit) return false;
            normal = hit.normal;
            return true;
        }

        bool HasHeadroom()
        {
            float r = config.capsuleRadius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (r + 0.05f);
            Vector3 top = transform.position + Vector3.up * (config.capsuleHeight - r);
            return !Physics.CheckCapsule(bottom, top, r, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        }

        void ApplyCapsule(bool standing)
        {
            if (_cc == null) return;
            float h = standing ? config.capsuleHeight : config.slideCapsuleHeight;
            _cc.height = h;
            _cc.radius = config.capsuleRadius;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }
    }
}
