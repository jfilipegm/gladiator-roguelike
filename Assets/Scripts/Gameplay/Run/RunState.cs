using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>One run's live numbers: timer, level/XP, gold, kills, pending level-ups.</summary>
    public sealed class RunState : MonoBehaviour, ITickable
    {
        public static RunState Current { get; private set; }

        public ProgressionConfig config;
        [Tooltip("XP gain multiplier (upgrades, The Nobody's Underdog later).")]
        public float xpMultiplier = 1f;

        public float Elapsed { get; private set; }
        public float TimeRemaining => Mathf.Max(0f, config.runDuration - Elapsed);
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public float XpToNext => config.XpForLevel(Level);
        public float Xp01 => Mathf.Clamp01(Xp / Mathf.Max(1f, XpToNext));
        public int Gold { get; private set; }
        public int Kills { get; private set; }
        public int PendingLevelUps { get; set; }

        void Awake() => Current = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            if (Current == this) Current = null;
        }

        void OnKill(in KillEvent e) => Kills++;

        public void Tick(float dt, float unscaledDt) => Elapsed += dt;

        public void AddXp(float amount)
        {
            Xp += amount * xpMultiplier;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingLevelUps++;
                GameEvents.OnLevelUp.Raise(new LevelUpEvent { NewLevel = Level });
            }
        }

        public void AddGold(int amount) => Gold += amount;
    }
}
