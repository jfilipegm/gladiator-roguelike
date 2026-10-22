using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Why the cards are open: the panel's title and the rarity floor.</summary>
    public enum PickSource : byte { LevelUp, Chest, Altar, Boss }

    /// <summary>
    /// Level-up flow (CLAUDE.md §6.5): XP threshold → short burst beat → pause → 3 cards (4 with Luck) rolled from the
    /// UpgradeCatalog → pick / reroll / banish / skip → resume (repeats for queued level-ups). Chests and the Blood
    /// Altar queue reward picks (M2.7) that open first, with a rarity floor. LevelUpPanel draws Offers; PlaytestBot
    /// picks through RunBotSensor hooks. Allocation-free.
    /// </summary>
    public sealed class LevelUpController : MonoBehaviour, ITickable
    {
        public static LevelUpController Instance { get; private set; }

        public RunState run;
        public PlayerBuild build;
        public DeathPopFx burst;
        [Tooltip("Real-time beat between the level-up burst and the cards (s).")]
        public float openDelay = 0.35f;
        [Tooltip("Real-time beat between using a chest/altar and its cards (s).")]
        public float rewardDelay = 0.3f;
        [Tooltip("Cards a level-up offers + this, clamped 1–4 (R7: the Meagre Choices edict, -1). Chests, the altar and the boss's reward are unchanged.")]
        public int levelUpCardDelta;

        public bool IsOpen { get; private set; }
        public readonly CardOffer[] Offers = new CardOffer[4];
        public int ChoiceCount { get; private set; }
        public int TimesChosen { get; private set; }
        /// <summary>Bumps whenever the offers change (open, reroll, banish) so the panel redraws.</summary>
        public int OffersVersion { get; private set; }
        /// <summary>What opened the current cards (title, rarity floor).</summary>
        public PickSource Source { get; private set; }
        public Rarity Floor { get; private set; }
        public int PendingRewards => _rewardCount;

        public UpgradeConfig Config => build != null ? build.config : null;
        public int RerollsLeft => build != null ? build.RerollsLeft : 0;
        public int BanishesLeft => build != null ? build.BanishesLeft : 0;

        readonly UpgradeRoller _roller = new UpgradeRoller();
        float _delay = -1f;
        struct Reward { public PickSource Source; public Rarity Floor; }
        readonly Reward[] _rewards = new Reward[8];
        int _rewardCount;
        // picks opened per source: the nth pick of a source reseeds the Cards stream with (source, n) (R8)
        readonly int[] _opens = new int[4];

        /// <summary>How many times cards opened for `s` this run (tests).</summary>
        public int OpensOf(PickSource s) => _opens[(int)s];

        /// <summary>Queues a pick from a chest or altar: it opens (paused) before any queued level-up. False if the queue is full.</summary>
        public bool QueueReward(PickSource source, Rarity floor)
        {
            if (_rewardCount >= _rewards.Length) return false;
            _rewards[_rewardCount++] = new Reward { Source = source, Floor = floor };
            return true;
        }

        void Awake() => Instance = this;

        void OnEnable()
        {
            Instance = this; // also after a disable/enable (tests and staged shots switch it off)
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
            _rewardCount = 0;
            if (IsOpen) Close();
            _delay = -1f;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (IsOpen || run == null || run.Ended) return;
            if (_rewardCount > 0) // a chest/altar you just used: a short beat (the lid pops), then its cards
            {
                if (_delay < 0f) _delay = rewardDelay;
                _delay -= unscaledDt;
                if (_delay <= 0f) Open(_rewards[0].Source, _rewards[0].Floor);
                return;
            }
            if (run.PendingLevelUps <= 0) return;
            if (_delay < 0f)
            {
                _delay = openDelay;
                if (burst != null && build != null)
                    burst.Burst(build.transform.position + Vector3.up * 0.9f, 28, 7f, 0.2f,
                        new Color(0.43f, 0.95f, 1f), new Color(1f, 0.86f, 0.4f));
            }
            _delay -= unscaledDt;
            if (_delay <= 0f) Open(PickSource.LevelUp, Rarity.Common);
        }

        float Luck => build != null ? build.Stats.Luck : 0f;

        void Open(PickSource source, Rarity floor)
        {
            // R8: the nth level-up (or chest, altar, boss reward) rolls the same cards however the run went; rerolls and
            // banishes continue this pick's own sequence
            GameRandom.Reseed(RngStream.Cards, ((uint)source << 16) | (uint)_opens[(int)source]++);
            Source = source;
            Floor = floor;
            _roller.MinRarity = floor;
            var c = Config;
            _roller.OfferEvolutions = c != null && (source == PickSource.LevelUp ? c.evolutionInLevelUps : c.evolutionInRewards);
            int n = c != null ? c.CardCount(Luck, GameRandom.Next01(RngStream.Cards)) : 3;
            if (source == PickSource.LevelUp) n = Mathf.Clamp(n + levelUpCardDelta, 1, 4);
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
            SfxPlayer.Play2D(SfxId.Pick);
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
            SfxPlayer.Play2D(SfxId.Card);
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
            SfxPlayer.Play2D(SfxId.Card);
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
            if (Source == PickSource.LevelUp) run.PendingLevelUps--;
            else if (_rewardCount > 0)
            {
                for (int i = 1; i < _rewardCount; i++) _rewards[i - 1] = _rewards[i];
                _rewardCount--;
            }
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
