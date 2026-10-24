using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena's boss beat (docs/BOSSES.md): when the run timer hits 0:00 the run waits instead of ending, the horde
    /// is dragged off, the announcer calls the boss, the camera turns to the gate and the boss walks in (or, with
    /// entranceRise, to the arena's centre, where he rises from below: the Eternal Victor). When he dies:
    /// slow motion, his fall line ("GORM FALLS!"), the hoard (a Rare+ pick), then the run ends as a victory. Dying still
    /// ends it as a loss. Words come from the BossDef. A BossDef with a revealText teases what his fall sets free (R3:
    /// "BELOW THE SAND, A CHAIN SNAPS." while Asterion is locked): the line comes revealDelay into the fall, and the fall
    /// is held revealHold longer so it's read before the hoard.
    /// </summary>
    public sealed class BossFight : MonoBehaviour, ITickable
    {
        [Tooltip("Tests: the old entrance (the horde dragged off, the Director off), so a boss test meets the boss alone. The game keeps the horde (note 38).")]
        public bool clearHorde;

        public enum Stage : byte { Waiting, Fight, Falling, Reward, Done }

        public BossDef boss;
        public BossController controller;
        [Tooltip("Which arena gate he comes through.")]
        public int gate;
        [Tooltip("Real seconds the camera spends turning toward him.")]
        public float cameraBeat = 2.6f;
        public float fallSlowMo = 0.25f;
        public float fallSeconds = 1.8f;

        public Stage Current { get; private set; }

        // note 25 (v0.27.0, "the bosses are too easy"): one log line per fight, for tuning from bot and playtest logs
        float _fightTime, _dealtAtStart, _takenInFight;
        int _hitsInFight;
        public float FightSeconds => _fightTime;
        public float DamageTakenInFight => _takenInFight;
        /// <summary>The boss's reveal line was raised during his fall (tests).</summary>
        public bool RevealShown { get; private set; }
        float _t;
        bool _revealChecked;
        PlayerCameraRig _rig;

        void Start()
        {
            if (RunState.Current != null) RunState.Current.holdForBoss = boss != null;
            var em = EnemyManager.Instance;
            if (em != null && boss != null && boss.enemy != null) em.Prewarm(boss.enemy, 1); // no allocation at 0:00
            _rig = FindAnyObjectByType<PlayerCameraRig>();
        }

        void OnEnable() { GameLoop.Register(this, TickGroup.Director, 60); GameEvents.OnDamaged.Subscribe(OnPlayerDamaged); }
        void OnDisable() { GameLoop.Unregister(this); GameEvents.OnDamaged.Unsubscribe(OnPlayerDamaged); }

        void OnPlayerDamaged(in DamagedEvent e)
        {
            if (Current != Stage.Fight) return;
            _takenInFight += e.Damage;
            _hitsInFight++;
        }

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
            if (run.Ended && Current != Stage.Done)
            {
                if (Current == Stage.Fight)
                    Debug.Log($"[Boss] {boss.id} won after {_fightTime:0.0}s: the player took {_takenInFight:0} in {_hitsInFight} hits, dealt {run.DamageDealt - _dealtAtStart:0}, level {run.Level}");
                Current = Stage.Done; TimeController.SlowMotion = 1f; return;
            }
            _t += unscaledDt;
            switch (Current)
            {
                case Stage.Waiting:
                    if (run.TimerDone) Begin();
                    break;
                case Stage.Fight:
                    _fightTime += dt;
                    if (_t < cameraBeat && _rig != null && controller.Alive) TurnCamera(unscaledDt);
                    if (controller.Defeated)
                    {
                        var hp = Player.Main != null ? Player.Main.GetComponent<PlayerHealth>() : null;
                        Debug.Log($"[Boss] {boss.id} down after {_fightTime:0.0}s: dealt {run.DamageDealt - _dealtAtStart:0} ({(run.DamageDealt - _dealtAtStart) / Mathf.Max(1f, _fightTime):0}/s), " +
                                  $"the player took {_takenInFight:0} in {_hitsInFight} hits, HP {(hp != null ? hp.Hp01 : 1f) * 100f:0}% left, level {run.Level}");
                        run.MarkBossDefeated(boss.id, hp != null ? hp.Hp01 : 1f);
                        if (boss.scatterOnDefeat && EnemyManager.Instance != null) EnemyManager.Instance.DespawnAll(); // the beasts scatter
                        Current = Stage.Falling;
                        _t = 0f;
                        _revealChecked = false;
                        TimeController.SlowMotion = fallSlowMo;
                        ScreenShake.Add(0.6f);
                        GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossFalls, Text = boss.fallText, Color = new Color(1f, 0.86f, 0.4f), Seconds = 3f });
                        if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
                    }
                    break;
                case Stage.Falling:
                    if (!_revealChecked && _t >= boss.revealDelay)
                    {
                        _revealChecked = true;
                        if (WantsReveal(boss)) Reveal();
                    }
                    if (_t >= fallSeconds + (RevealShown ? boss.revealHold : 0f))
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
            _fightTime = 0f;
            _takenInFight = 0f;
            _hitsInFight = 0;
            _dealtAtStart = RunState.Current != null ? RunState.Current.DamageDealt : 0f;
            // note 38 (the producer, v0.28.0: "the boss fights are too easy, don't remove the mobs nor their spawning"): the
            // horde stays and keeps coming, a little slower, without swarms or elites (Director.BossPhase)
            var em = EnemyManager.Instance;
            if (clearHorde)
            {
                if (Director.Instance != null) Director.Instance.enabled = false;
                if (em != null) em.DespawnAll(); // the old entrance: the horde dragged off (no XP shower)
                if (PickupSystem.Instance != null) PickupSystem.Instance.ClearAll();
                if (EnemyProjectiles.Instance != null) EnemyProjectiles.Instance.Clear();
            }
            else if (Director.Instance != null) Director.Instance.BossPhase = true;
            controller.def = boss;
            // the Eternal Victor rises from below at the arena's centre (the Colosseum's stage lift): no gate opens; the
            // gate is the fallback should he not spawn there
            if (!(boss.entranceRise && em != null && controller.BeginRise(em.arenaCenter)))
            {
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
                    at = em != null ? em.EdgePoint(Mathf.PI * 0.5f, 2f) : Vector3.forward * 15f; // the north edge
                    inward = -at.normalized;
                }
                controller.Begin(at, inward);
            }
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossEnters, Text = boss.enterText, Color = new Color(1f, 0.3f, 0.2f), Seconds = 3f });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.85f);
        }

        /// <summary>A reveal line, and its unlock (if any) not earned yet.</summary>
        static bool WantsReveal(BossDef b) =>
            !string.IsNullOrEmpty(b.revealText) && (b.revealUnlock == null || !MetaProgress.IsUnlocked(b.revealUnlock));

        void Reveal()
        {
            RevealShown = true;
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = boss.revealText, Color = boss.revealColor, Seconds = 2.6f });
            var em = EnemyManager.Instance;
            Vector3 at = em != null ? em.arenaCenter : Vector3.zero; // below the stage
            SfxPlayer.Play(SfxId.Rattle, at);
            SfxPlayer.Play(SfxId.BossRoar, at);
            ScreenShake.Add(0.15f);
        }

        void TurnCamera(float dt)
        {
            Vector3 d = controller.Position - Player.Main.transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _rig.SetAngles(Mathf.MoveTowardsAngle(_rig.Yaw, yaw, 150f * dt), _rig.Pitch);
        }
    }
}
