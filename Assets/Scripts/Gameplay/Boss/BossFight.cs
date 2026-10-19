using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena's boss beat (docs/BOSSES.md): when the run timer hits 0:00 the run waits instead of ending, the horde
    /// is dragged off, the announcer calls the boss, the camera turns to the gate and the boss walks in. When he dies:
    /// slow motion, "GORM FALLS!", the hoard (a Rare+ pick), then the run ends as a victory. Dying still ends it as a loss.
    /// </summary>
    public sealed class BossFight : MonoBehaviour, ITickable
    {
        public enum Stage : byte { Waiting, Fight, Falling, Reward, Done }

        public BossDef boss;
        public BossController controller;
        [Tooltip("Which arena gate he comes through.")]
        public int gate;
        [Tooltip("Real seconds the camera spends turning toward him.")]
        public float cameraBeat = 2.6f;
        public float fallSlowMo = 0.25f;
        public float fallSeconds = 1.8f;

        const string EnterText = "THE PIT MASTER ENTERS!";
        const string FallText = "GORM FALLS!";

        public Stage Current { get; private set; }
        float _t;
        PlayerCameraRig _rig;

        void Start()
        {
            if (RunState.Current != null) RunState.Current.holdForBoss = boss != null;
            var em = EnemyManager.Instance;
            if (em != null && boss != null && boss.enemy != null) em.Prewarm(boss.enemy, 1); // no allocation at 0:00
            _rig = FindAnyObjectByType<PlayerCameraRig>();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Director, 60);
        void OnDisable() => GameLoop.Unregister(this);

        /// <summary>Starts the boss now (admin "boss now", tests): jumps the run timer to its end.</summary>
        public void StartNow()
        {
            var run = RunState.Current;
            if (run != null) run.SetTimeRemaining(0f);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var run = RunState.Current;
            if (run == null || boss == null || controller == null) return;
            if (run.Ended && Current != Stage.Done) { Current = Stage.Done; TimeController.SlowMotion = 1f; return; }
            _t += unscaledDt;
            switch (Current)
            {
                case Stage.Waiting:
                    if (run.TimerDone) Begin();
                    break;
                case Stage.Fight:
                    if (_t < cameraBeat && _rig != null && controller.Alive) TurnCamera(unscaledDt);
                    if (controller.Defeated)
                    {
                        run.MarkBossDefeated(boss.id);
                        Current = Stage.Falling;
                        _t = 0f;
                        TimeController.SlowMotion = fallSlowMo;
                        ScreenShake.Add(0.6f);
                        GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossFalls, Text = FallText, Color = new Color(1f, 0.86f, 0.4f), Seconds = 3f });
                        if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
                    }
                    break;
                case Stage.Falling:
                    if (_t >= fallSeconds)
                    {
                        TimeController.SlowMotion = 1f;
                        var lvl = LevelUpController.Instance;
                        if (lvl != null && lvl.isActiveAndEnabled) lvl.QueueReward(PickSource.Boss, boss.rewardFloor);
                        Current = Stage.Reward;
                    }
                    break;
                case Stage.Reward:
                {
                    var lvl = LevelUpController.Instance;
                    bool waiting = lvl != null && lvl.isActiveAndEnabled && (lvl.IsOpen || lvl.PendingRewards > 0);
                    if (!waiting) { run.End(RunOutcome.Survived); Current = Stage.Done; }
                    break;
                }
            }
        }

        void Begin()
        {
            Current = Stage.Fight;
            _t = 0f;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var em = EnemyManager.Instance;
            if (em != null) em.DespawnAll(); // the horde is dragged off (no XP shower: no cards mid-entrance)
            if (PickupSystem.Instance != null) PickupSystem.Instance.ClearAll();
            if (EnemyProjectiles.Instance != null) EnemyProjectiles.Instance.Clear();
            var gates = ArenaGates.Instance;
            Vector3 at, inward;
            if (gates != null && gates.Count > 0)
            {
                int g = Mathf.Clamp(gate, 0, gates.Count - 1);
                at = gates.Position(g);
                inward = gates.Inward(g);
                gates.Open(g, 4f);
            }
            else
            {
                at = em != null ? em.arenaCenter + Vector3.forward * (em.arenaRadius - 2f) : Vector3.forward * 15f;
                inward = -at.normalized;
            }
            controller.def = boss;
            controller.Begin(at, inward);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossEnters, Text = EnterText, Color = new Color(1f, 0.3f, 0.2f), Seconds = 3f });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.85f);
        }

        void TurnCamera(float dt)
        {
            Vector3 d = controller.Position - Player.Main.transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _rig.SetAngles(Mathf.MoveTowardsAngle(_rig.Yaw, yaw, 150f * dt), _rig.Pitch);
        }
    }
}
