using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    public enum RunOutcome { None, Died, Survived }

    /// <summary>One run's live numbers: timer, level/XP, gold, kills, pending level-ups, and how it ended.</summary>
    public sealed class RunState : MonoBehaviour, ITickable
    {
        public static RunState Current { get; private set; }

        public ProgressionConfig config;
        [Tooltip("This arena's tier (0 = the Slave Pit): Renown bonus, deepest arena, ReachArena unlocks. Set by ArenaBuilder.")]
        public int arenaTier;

        public float Elapsed { get; private set; }
        public float TimeRemaining => Mathf.Max(0f, config.runDuration - Elapsed);
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext => config.XpForLevel(Level);
        public float Xp01 => Mathf.Clamp01(Xp / Mathf.Max(1f, XpToNext));
        public int Gold { get; private set; }
        /// <summary>All gold picked up this run (Gold minus what chests cost is what's left).</summary>
        public int GoldEarned { get; private set; }
        /// <summary>BossDef.id of the boss beaten this run (null = none yet).</summary>
        public string BossDefeated { get; private set; }
        public int Kills { get; private set; }
        public float DamageDealt { get; private set; }
        public int PendingLevelUps { get; set; }
        public RunOutcome Outcome { get; private set; }
        public bool Ended => Outcome != RunOutcome.None;
        [Tooltip("When the timer runs out, wait for the boss fight instead of ending the run (set by BossFight).")]
        public bool holdForBoss;
        /// <summary>The run timer reached zero (boss time).</summary>
        public bool TimerDone { get; private set; }

        void Awake() => Current = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late);
            GameEvents.OnKill.Subscribe(OnKill);
            GameEvents.OnHit.Subscribe(OnHit);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            GameEvents.OnHit.Unsubscribe(OnHit);
            if (Current == this) Current = null;
        }

        // Kills per source (weapon RuntimeId) for "kill N with weapon W" unlocks: a fixed table, no allocation.
        const int MaxSources = 16;
        readonly int[] _sourceIds = new int[MaxSources];
        readonly int[] _sourceKills = new int[MaxSources];
        int _sourceCount;

        void OnKill(in KillEvent e)
        {
            if (Ended) return;
            Kills++;
            for (int k = 0; k < _sourceCount; k++)
                if (_sourceIds[k] == e.SourceId) { _sourceKills[k]++; return; }
            if (_sourceCount < MaxSources) { _sourceIds[_sourceCount] = e.SourceId; _sourceKills[_sourceCount++] = 1; }
        }

        public int KillsBy(int sourceId)
        {
            for (int k = 0; k < _sourceCount; k++) if (_sourceIds[k] == sourceId) return _sourceKills[k];
            return 0;
        }

        /// <summary>The boss of this arena died (BossFight). Recorded even if the player dies afterwards.</summary>
        /// <summary>The player's HP fraction when the boss fell (1 = none fell; unlock "beat X below N % HP").</summary>
        public float BossDefeatedHp01 { get; private set; } = 1f;

        public void MarkBossDefeated(string bossId, float playerHp01 = 1f)
        {
            if (Ended || string.IsNullOrEmpty(bossId)) return;
            BossDefeated = bossId;
            BossDefeatedHp01 = Mathf.Clamp01(playerHp01);
        }
        void OnHit(in HitEvent e) { if (!Ended) DamageDealt += e.Damage; }

        public void Tick(float dt, float unscaledDt)
        {
            if (Ended || DevSettings.Timeless) return;
            Elapsed += dt;
            if (Elapsed >= config.runDuration)
            {
                if (holdForBoss) TimerDone = true; // the boss fight decides the run (BossFight)
                else End(RunOutcome.Survived);
            }
        }

        /// <summary>Ends the run once (later calls are ignored) and raises OnRunEnd.</summary>
        public void End(RunOutcome outcome)
        {
            if (Ended || outcome == RunOutcome.None) return;
            Outcome = outcome;
            Debug.Log($"[RunState] run ended: {outcome} at {Elapsed:0.0}s, level {Level}, kills {Kills}");
            GameEvents.OnRunEnd.Raise(new RunEndEvent { Outcome = outcome, Elapsed = Elapsed });
        }

        /// <summary>Dev/admin: jump the countdown (e.g. to 0:10 to see the timer end).</summary>
        public void SetTimeRemaining(float seconds) => Elapsed = Mathf.Clamp(config.runDuration - seconds, 0f, config.runDuration);

        /// <summary>Adds XP as given (XP Gain is applied by whoever awards it — PickupSystem for gems).</summary>
        public void AddXp(float amount)
        {
            if (Ended) return;
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingLevelUps++;
                GameEvents.OnLevelUp.Raise(new LevelUpEvent { NewLevel = Level });
            }
        }

        public void AddGold(int amount)
        {
            if (Ended) return;
            Gold += amount;
            if (amount > 0) GoldEarned += amount;
        }

        /// <summary>Pays `amount` if there's enough (chests). False = not enough gold, nothing spent.</summary>
        public bool SpendGold(int amount)
        {
            if (Ended || amount < 0 || Gold < amount) return false;
            Gold -= amount;
            return true;
        }
    }
}
