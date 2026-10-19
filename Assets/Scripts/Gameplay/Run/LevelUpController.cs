using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Level-up flow (CLAUDE.md §6.5): XP threshold → short burst beat → pause → 3 cards (4 with Luck) rolled from the
    /// UpgradeCatalog → pick / reroll / banish / skip → resume (repeats for queued level-ups). LevelUpPanel draws
    /// Offers; PlaytestBot picks through RunBotSensor hooks. Allocation-free.
    /// </summary>
    public sealed class LevelUpController : MonoBehaviour, ITickable
    {
        public static LevelUpController Instance { get; private set; }

        public RunState run;
        public PlayerBuild build;
        public DeathPopFx burst;
        [Tooltip("Real-time beat between the level-up burst and the cards (s).")]
        public float openDelay = 0.35f;

        public bool IsOpen { get; private set; }
        public readonly CardOffer[] Offers = new CardOffer[4];
        public int ChoiceCount { get; private set; }
        public int TimesChosen { get; private set; }
        /// <summary>Bumps whenever the offers change (open, reroll, banish) so the panel redraws.</summary>
        public int OffersVersion { get; private set; }

        public UpgradeConfig Config => build != null ? build.config : null;
        public int RerollsLeft => build != null ? build.RerollsLeft : 0;
        public int BanishesLeft => build != null ? build.BanishesLeft : 0;

        readonly UpgradeRoller _roller = new UpgradeRoller();
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

        // The run is over: drop the open cards (nothing applied) and anything queued.
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
                if (burst != null && build != null)
                    burst.Burst(build.transform.position + Vector3.up * 0.9f, 28, 7f, 0.2f,
                        new Color(0.43f, 0.95f, 1f), new Color(1f, 0.86f, 0.4f));
            }
            _delay -= unscaledDt;
            if (_delay <= 0f) Open();
        }

        float Luck => build != null ? build.Stats.Luck : 0f;

        void Open()
        {
            var c = Config;
            int n = c != null ? c.CardCount(Luck, GameRandom.Value) : 3;
            ChoiceCount = RollAll(n);
            OffersVersion++;
            IsOpen = true;
            TimeController.PushPause();
            if (Player.Main != null) Player.Main.BlockInput();
        }

        int RollAll(int n)
        {
            if (build == null || build.catalog == null) return 0;
            return _roller.Roll(build.catalog.upgrades, build.catalog.fallbacks, build.State, build.config, Luck, Offers, n);
        }

        /// <summary>Takes card `index`, closes (the next queued level-up opens after the beat).</summary>
        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= ChoiceCount) return;
            build.Take(Offers[index]);
            TimesChosen++;
            Finish();
        }

        /// <summary>New cards for the same level-up (limited per run). Returns false if none left.</summary>
        public bool Reroll()
        {
            if (!IsOpen || RerollsLeft <= 0) return false;
            build.State.RerollsUsed++;
            int n = ChoiceCount;
            ChoiceCount = RollAll(n);
            OffersVersion++;
            return true;
        }

        /// <summary>Removes card `index`'s upgrade from this run's pool and replaces the card (limited per run).</summary>
        public bool Banish(int index)
        {
            if (!IsOpen || BanishesLeft <= 0 || index < 0 || index >= ChoiceCount) return false;
            var d = Offers[index].Def;
            if (d == null || d.kind == UpgradeKind.Consumable) return false;
            build.State.BanishesUsed++;
            build.State.Banish(d);
            ChoiceCount = _roller.Replace(build.catalog.upgrades, build.catalog.fallbacks, build.State, build.config, Luck, Offers, ChoiceCount, index);
            OffersVersion++;
            if (ChoiceCount == 0) Skip();
            return true;
        }

        /// <summary>Takes nothing this level.</summary>
        public void Skip()
        {
            if (!IsOpen) return;
            Finish();
        }

        void Finish()
        {
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
    }
}
