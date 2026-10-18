using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>M1.3 stub boons (the real data-driven upgrade/card system replaces this in M2.4).</summary>
    public enum StubBoon { Damage, AttackSpeed, MoveSpeed, MaxHp, Magnet, Range, Crit }

    public struct LevelUpChoice
    {
        public StubBoon Boon;
        public string Title;
        public string Description;
    }

    /// <summary>
    /// Level-up flow (stub): XP threshold → short burst beat → pause → N cards → apply → resume (repeats for queued
    /// level-ups). UI reads IsOpen/Choices; PlaytestBot picks through RunBotSensor hooks.
    /// </summary>
    public sealed class LevelUpController : MonoBehaviour, ITickable
    {
        public static LevelUpController Instance { get; private set; }

        public RunState run;
        public WeaponSystem weapons;
        public PlayerMotor motor;
        public PlayerHealth health;
        public PickupSystem pickups;
        public DeathPopFx burst;
        [Tooltip("Real-time beat between the level-up burst and the cards (s).")]
        public float openDelay = 0.35f;

        static readonly LevelUpChoice[] Table =
        {
            new LevelUpChoice { Boon = StubBoon.Damage, Title = "Brute Force", Description = "+15% damage" },
            new LevelUpChoice { Boon = StubBoon.AttackSpeed, Title = "Quick Hands", Description = "+12% attack speed" },
            new LevelUpChoice { Boon = StubBoon.MoveSpeed, Title = "Light Feet", Description = "+8% move speed" },
            new LevelUpChoice { Boon = StubBoon.MaxHp, Title = "Thick Skin", Description = "+20 max HP, heal 20" },
            new LevelUpChoice { Boon = StubBoon.Magnet, Title = "Greedy", Description = "+30% pickup range" },
            new LevelUpChoice { Boon = StubBoon.Range, Title = "Long Reach", Description = "+12% weapon reach" },
            new LevelUpChoice { Boon = StubBoon.Crit, Title = "Lucky Cut", Description = "+6% crit chance" },
        };

        public bool IsOpen { get; private set; }
        public readonly LevelUpChoice[] Choices = new LevelUpChoice[4];
        public int ChoiceCount { get; private set; }
        public int TimesChosen { get; private set; }

        float _delay = -1f;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late, 10);
            GameEvents.OnRunEnd.Subscribe(OnRunEnd);
            RunBotSensor.PendingCards = BotPending;
            RunBotSensor.ChooseCardHook = Choose;
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnRunEnd.Unsubscribe(OnRunEnd);
            if (IsOpen) Close();
            if (Instance == this) Instance = null;
        }

        int BotPending() => IsOpen ? ChoiceCount : 0;

        // The run is over: drop the open card (nothing applied) and anything queued.
        void OnRunEnd(in RunEndEvent e)
        {
            if (run != null) run.PendingLevelUps = 0;
            if (IsOpen) Close();
            _delay = -1f;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (IsOpen || run == null || run.PendingLevelUps <= 0 || run.Ended) return;
            if (_delay < 0f)
            {
                _delay = openDelay;
                if (burst != null && motor != null)
                    burst.Burst(motor.transform.position + Vector3.up * 0.9f, 28, 7f, 0.2f,
                        new Color(0.43f, 0.95f, 1f), new Color(1f, 0.86f, 0.4f));
            }
            _delay -= unscaledDt;
            if (_delay <= 0f) Open();
        }

        void Open()
        {
            int n = Mathf.Clamp(run.config.cardsPerLevelUp, 1, Choices.Length);
            ChoiceCount = n;
            // distinct random picks (partial Fisher-Yates over table indices, allocation-free)
            System.Span<int> idx = stackalloc int[Table.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            for (int i = 0; i < n; i++)
            {
                int j = GameRandom.Range(i, idx.Length);
                (idx[i], idx[j]) = (idx[j], idx[i]);
                Choices[i] = Table[idx[i]];
            }
            IsOpen = true;
            TimeController.PushPause();
            if (Player.Main != null) Player.Main.BlockInput();
        }

        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= ChoiceCount) return;
            Apply(Choices[index].Boon);
            TimesChosen++;
            run.PendingLevelUps--;
            Close();
        }

        void Close()
        {
            IsOpen = false;
            _delay = -1f;
            TimeController.PopPause();
            if (Player.Main != null) Player.Main.UnblockInput();
        }

        void Apply(StubBoon b)
        {
            switch (b)
            {
                case StubBoon.Damage: weapons.damageMultiplier *= 1.15f; break;
                case StubBoon.AttackSpeed: weapons.attackSpeedMultiplier *= 1.12f; break;
                case StubBoon.MoveSpeed: motor.SpeedMultiplier *= 1.08f; break;
                case StubBoon.MaxHp: health.maxHp += 20f; health.Heal(20f); break;
                case StubBoon.Magnet: pickups.magnetMultiplier *= 1.3f; break;
                case StubBoon.Range: weapons.rangeMultiplier *= 1.12f; break;
                case StubBoon.Crit: weapons.critChanceBonus += 0.06f; break;
            }
        }
    }
}
