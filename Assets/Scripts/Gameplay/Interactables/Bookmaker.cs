using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Provincial Pit's secret (Phase 5.6, docs/UNLOCKS.md): a hunched bookmaker at one of the layout's secret spots.
    /// Come close and he calls you over; step into his gold chalk ring and press Interact (E / gamepad North, like the
    /// chests; the HUD shows "E  WAGER 30 GOLD" over him) and he takes a stake and bets you can't beat your own pace:
    /// kill `paceFactor` × your kills of the last 30 s (at least `minKills`) in `seconds`. The HUD counts it down
    /// (RunObjective). Win: he pays `payout` gold and, the first time, the flag `bookmaker_paid` (Loaded Dice's
    /// unlock) is saved at once. Lose: the house keeps the stake. One wager per run.
    /// </summary>
    public sealed class Bookmaker : MonoBehaviour, ITickable
    {
        public enum Phase : byte { Absent, Waiting, Wager, Paid, Lost }

        public GameObject model;
        public Material material;
        public Vector3[] spots = System.Array.Empty<Vector3>();
        public Vector3 arenaCenter;
        public string flag = "bookmaker_paid";

        [Header("The wager")]
        public int stake = 30;
        public int payout = 90;
        public float seconds = 30f;
        [Tooltip("The bet: this × your kills of the last 30 s…")]
        public float paceFactor = 1.3f;
        [Tooltip("…but never fewer, nor more, than these.")]
        public int minKills = 60, maxKills = 600;

        [Header("The booth")]
        public float callRange = 9f;
        [Tooltip("His spot: Interact takes the bet from inside this ring (it pulses while you're in it).")]
        public float ringRadius = 1.3f;
        [Tooltip("His chalk ring (review: a pale gold vanished on the sand; deeper and thicker reads).")]
        public Color ringColor = new Color(1f, 0.7f, 0.1f, 0.95f);
        public float ringThickness = 0.16f;
        public float favor = 30f;

        public const string CallText = "PSST! A WAGER, CHAMPION?";
        public const string BetText = "THE BET IS ON: BEAT YOUR OWN PACE!";
        public const string BrokeText = "NO GOLD, NO WAGER.";
        public const string WinText = "THE BOOKMAKER PAYS UP!";
        public const string FirstWinText = "WHAT A SHARP! HE PAYS UP!";
        public const string LoseText = "THE HOUSE WINS.";
        public const string ObjectiveLabel = "WAGER";
        public Color textColor = new Color(1f, 0.84f, 0.3f);

        Transform _view;
        Phase _phase = Phase.Absent;
        Vector3 _pos;
        float _left;
        bool _called;
        int _startKills;

        // kills per second over the last 30 s (one bucket per second of run time)
        readonly int[] _buckets = new int[30];
        int _bucketSecond = -1;

        public Phase State => _phase;
        public Vector3 Position => _pos;
        public int Target { get; private set; }
        public int Progress { get; private set; }
        /// <summary>He's waiting and the player is within his call: the HUD shows the wager over his head.</summary>
        public bool Calling { get; private set; }
        /// <summary>The player stands in his ring: Interact takes the bet (the HUD's bottom prompt).</summary>
        public bool InReach { get; private set; }
        /// <summary>The player has the stake.</summary>
        public bool CanAfford => RunState.Current != null && RunState.Current.Gold >= stake;
        public static Bookmaker Instance { get; private set; }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Pickups, 22);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            if (Instance == this) Instance = null;
            if (_phase == Phase.Wager) RunObjective.Clear();
        }

        void Start()
        {
            if (spots == null || spots.Length == 0) return;
            Place(Random.Range(0, spots.Length)); // not GameRandom: that one replays the same rolls every launch
        }

        /// <summary>Puts him at spot `index`, waiting for a customer (tests pick one).</summary>
        public void Place(int index)
        {
            _pos = spots[Mathf.Clamp(index, 0, spots.Length - 1)];
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _pos.y = em.Height.Sample(_pos.x, _pos.z);
            if (_view == null) _view = Spawn();
            _view.position = _pos;
            Vector3 toCenter = arenaCenter - _pos;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f) _view.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
            _view.gameObject.SetActive(true);
            _phase = Phase.Waiting;
            _called = false;
            Calling = InReach = false;
        }

        Transform Spawn()
        {
            var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); }
            go.name = "Bookmaker";
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go.transform;
        }

        void OnKill(in KillEvent e)
        {
            var run = RunState.Current;
            if (run == null) return;
            Roll(run.Elapsed);
            _buckets[_bucketSecond % _buckets.Length]++;
        }

        /// <summary>Advances the 30 one-second buckets to `elapsed`, emptying the seconds that went by.</summary>
        void Roll(float elapsed)
        {
            int sec = Mathf.FloorToInt(elapsed);
            if (_bucketSecond < 0) _bucketSecond = sec;
            for (int s = _bucketSecond + 1; s <= sec && s <= _bucketSecond + _buckets.Length; s++) _buckets[s % _buckets.Length] = 0;
            if (sec > _bucketSecond) _bucketSecond = sec;
        }

        /// <summary>Kills in the last 30 s of run time.</summary>
        public int RecentKills()
        {
            var run = RunState.Current;
            if (run != null) Roll(run.Elapsed);
            int n = 0;
            for (int k = 0; k < _buckets.Length; k++) n += _buckets[k];
            return n;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            if (_phase != Phase.Waiting) Calling = InReach = false;
            if (_phase == Phase.Waiting) TickWaiting(dt);
            else if (_phase == Phase.Wager) TickWager(dt);
        }

        void TickWaiting(float dt)
        {
            var player = Player.Main;
            var run = RunState.Current;
            if (player == null) return;
            Vector3 d = player.transform.position - _pos;
            float dist = new Vector2(d.x, d.z).magnitude;
            bool open = run != null && !RunState.CombatOver;
            Calling = open && dist < callRange;
            InReach = open && dist < ringRadius && Mathf.Abs(d.y) < 1.5f;
            var tele = TelegraphRenderer.Instance;
            if (tele != null) tele.Circle(_pos, ringRadius, InReach ? 1f : 0f, ringColor, TelegraphRenderer.Style.Ring, ringThickness, 0.31f);
            if (dist < callRange)
            {
                Vector3 look = new Vector3(d.x, 0f, d.z);
                if (look.sqrMagnitude > 0.01f) _view.rotation = Quaternion.Slerp(_view.rotation, Quaternion.LookRotation(look, Vector3.up), dt * 4f);
                if (!_called)
                {
                    _called = true;
                    SfxPlayer.Play(SfxId.Quill, _pos);
                    Say(CallText, 2.4f);
                }
            }
            // E in his ring, like a chest. If a chest is in reach too, the chest takes the press (Interactables ticks after
            // us and reads the same press; its InReach is from last frame), so one press never spends gold twice.
            var props = Interactables.Instance;
            if (InReach && player.Input.InteractPressed && (props == null || props.InReach < 0)) TryBet();
        }

        /// <summary>Takes the stake and posts the bet (Interact in his ring, or tests). False without enough gold.</summary>
        public bool TryBet()
        {
            if (_phase != Phase.Waiting) return false;
            var run = RunState.Current;
            if (run == null || !run.SpendGold(stake))
            {
                Say(BrokeText, 1.8f);
                return false;
            }
            Calling = InReach = false;
            Target = Mathf.Clamp(Mathf.CeilToInt(RecentKills() * paceFactor), minKills, maxKills);
            _startKills = run.Kills;
            _left = seconds;
            Progress = 0;
            _phase = Phase.Wager;
            RunObjective.Begin(ObjectiveLabel, Target, seconds);
            SfxPlayer.Play(SfxId.Coin, _pos);
            SfxPlayer.Play(SfxId.Quill, _pos);
            Say(BetText, 2.2f);
            return true;
        }

        void TickWager(float dt)
        {
            var run = RunState.Current;
            if (run == null) return;
            _left -= dt;
            Progress = run.Kills - _startKills;
            RunObjective.Set(Progress, _left);
            if (Progress >= Target) Win(run);
            else if (_left <= 0f || run.Ended) Lose();
        }

        void Win(RunState run)
        {
            _phase = Phase.Paid;
            RunObjective.Clear();
            run.AddGold(payout);
            var player = Player.Main;
            Vector3 at = player != null ? player.transform.position : _pos;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 1f, 24, 5f, 0.16f, new Color(1f, 0.9f, 0.45f), new Color(1f, 1f, 0.85f));
            SfxPlayer.Play2D(SfxId.Coin);
            SfxPlayer.Play2D(SfxId.Cheer);
            if (Favor.Instance != null) Favor.Instance.Add(favor);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.5f);
            bool first = MetaProgress.SetFlag(flag);
            Say(first ? FirstWinText : WinText, 2.4f);
        }

        void Lose()
        {
            _phase = Phase.Lost;
            RunObjective.Clear();
            SfxPlayer.Play2D(SfxId.Boo);
            Say(LoseText, 2f);
        }

        void Say(string text, float secs) =>
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = text, Color = textColor, Seconds = secs, Kind = AnnounceKind.Line });
    }
}
