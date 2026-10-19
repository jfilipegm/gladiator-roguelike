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

        public float Elapsed { get; private set; }
        public float TimeRemaining => Mathf.Max(0f, config.runDuration - Elapsed);
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext => config.XpForLevel(Level);
        public float Xp01 => Mathf.Clamp01(Xp / Mathf.Max(1f, XpToNext));
        public int Gold { get; private set; }
        public int Kills { get; private set; }
        public float DamageDealt { get; private set; }
        public int PendingLevelUps { get; set; }
        public RunOutcome Outcome { get; private set; }
        public bool Ended => Outcome != RunOutcome.None;

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

        void OnKill(in KillEvent e) { if (!Ended) Kills++; }
        void OnHit(in HitEvent e) { if (!Ended) DamageDealt += e.Damage; }

        public void Tick(float dt, float unscaledDt)
        {
            if (Ended || DevSettings.Timeless) return;
            Elapsed += dt;
            if (Elapsed >= config.runDuration) End(RunOutcome.Survived);
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

        public void AddGold(int amount) { if (!Ended) Gold += amount; }
    }
}
