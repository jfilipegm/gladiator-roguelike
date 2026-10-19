using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Arena Master's whims (docs/FAVOR.md): when the show gets dull — few kills for the horde's size and nothing
    /// near the player for a while — boredom builds, the crowd boos, and the master forces the action: a gate thrown
    /// open (a swarm through the gate nearest the player) or a champion's challenge (an elite with a bounty if it dies
    /// fast). Never early in the run, never during the boss, never twice inside the cooldown. No allocation.
    /// </summary>
    public sealed class ArenaMaster : MonoBehaviour, ITickable
    {
        public static ArenaMaster Instance { get; private set; }

        public FavorConfig config;

        public const string RestlessText = "THE CROWD GROWS RESTLESS!";
        public const string ChallengeText = "A CHAMPION'S CHALLENGE!";
        public const string ChallengeWonText = "CHALLENGE WON!";
        public const string ChallengeLostText = "THE CROWD JEERS!";
        static readonly Color Danger = new Color(1f, 0.38f, 0.2f);
        static readonly Color Gold = new Color(1f, 0.86f, 0.4f);

        const int Ring = 1024;
        readonly float[] _kills = new float[Ring];
        int _head, _count;
        float _clock, _lastWhim = -9999f;
        uint _rng = 0xB0BA5u;

        int _championUid = -1, _championSlot = -1;
        float _championUntil;

        /// <summary>0–100; a whim fires at 100.</summary>
        public float Boredom { get; private set; }
        public int WhimsFired { get; private set; }
        public int ChallengesWon { get; private set; }
        public bool ChallengeActive => _championUid >= 0;
        /// <summary>Last frame's dull test (tests, debug).</summary>
        public bool Dull { get; private set; }

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Director, 5);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            if (Instance == this) Instance = null;
        }

        float Rand01()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000;
        }

        void OnKill(in KillEvent e)
        {
            _kills[_head] = _clock;
            _head = (_head + 1) % Ring;
            if (_count < Ring) _count++;
            if (_championUid >= 0 && e.TargetId == _championUid) ChampionDown(e.Position);
        }

        int KillsInWindow(float window)
        {
            int n = 0;
            for (int k = 1; k <= _count; k++)
            {
                if (_clock - _kills[(_head - k + Ring) % Ring] > window) break;
                n++;
            }
            return n;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (config == null || dt <= 0f) return;
            _clock += dt;
            var run = RunState.Current;
            var em = EnemyManager.Instance;
            if (run == null || run.Ended || em == null || Player.Main == null) return;
            if (_championUid >= 0) TickChallenge(em);

            var boss = BossController.Active;
            bool bossOn = boss != null && !boss.Defeated || run.TimerDone;
            Vector3 player = Player.Main.transform.position;
            int target = Director.Instance != null && Director.Instance.isActiveAndEnabled ? Director.Instance.TargetAlive : em.AliveCount;
            float nearest = em.TryGetNearest(player, config.dullEnemyDistance, out _) ? 0f : float.MaxValue;
            Dull = !bossOn && config.IsDull(KillsInWindow(config.dullWindow), target, nearest);
            Boredom = Mathf.Clamp(Boredom + (Dull ? config.boredomRise : -config.boredomFall) * dt, 0f, 100f);
            if (Boredom >= 100f && config.WhimAllowed(run.Elapsed, _clock - _lastWhim, bossOn)) FireWhim();
        }

        /// <summary>Fires the next whim now (boredom full; admin and tests). Returns false if none could run.</summary>
        public bool FireWhim()
        {
            var em = EnemyManager.Instance;
            if (config == null || em == null || Player.Main == null || config.whims == null || config.whims.Length == 0) return false;
            var whim = config.whims[Mathf.Min(config.whims.Length - 1, (int)(Rand01() * config.whims.Length))];
            if (whim == WhimKind.ChampionChallenge && _championUid >= 0) whim = WhimKind.GatesOpen; // one challenge at a time
            bool ok = whim == WhimKind.GatesOpen ? OpenGates(em) : StartChallenge(em);
            if (!ok) return false;
            Boredom = 0f;
            _lastWhim = _clock;
            WhimsFired++;
            SfxPlayer.Play2D(SfxId.Boo);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = whim == WhimKind.GatesOpen ? RestlessText : ChallengeText, Color = Danger, Seconds = 2.2f, Kind = AnnounceKind.Whim });
            GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.WhimFired, Whim = whim, Position = Player.Main.transform.position });
            return true;
        }

        bool OpenGates(EnemyManager em)
        {
            var director = Director.Instance;
            if (director == null) return false;
            var gates = ArenaGates.Instance;
            int gate = gates != null && gates.Count > 0 ? gates.Nearest(Player.Main.transform.position) : -1;
            return director.StartSwarmThrough(gate);
        }

        bool StartChallenge(EnemyManager em)
        {
            var director = Director.Instance;
            if (director == null) return false;
            Vector3 player = Player.Main.transform.position;
            Vector3 spot = player;
            bool found = false;
            for (int attempt = 0; attempt < 12 && !found; attempt++)
            {
                float a = Rand01() * Mathf.PI * 2f;
                spot = player + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * config.challengeDistance;
                float dx = spot.x - em.arenaCenter.x, dz = spot.z - em.arenaCenter.z;
                if (dx * dx + dz * dz > (em.arenaRadius - 2f) * (em.arenaRadius - 2f)) continue;
                if (em.Obstacles != null && em.Obstacles.Overlaps(new Vector2(spot.x, spot.z), 1f)) continue;
                found = true;
            }
            if (!found) spot = em.arenaCenter;
            int slot = director.SpawnEliteAt(spot);
            if (slot < 0) return false;
            _championSlot = slot;
            _championUid = em.GetUid(slot);
            _championUntil = _clock + config.challengeSeconds;
            return true;
        }

        void TickChallenge(EnemyManager em)
        {
            if (!em.IsAlive(_championSlot) || em.GetUid(_championSlot) != _championUid) { _championUid = -1; return; } // gone without a kill event
            var tele = TelegraphRenderer.Instance;
            float left = _championUntil - _clock;
            if (tele != null) tele.Circle(em.GetPosition(_championSlot), 1.6f, Mathf.Clamp01(1f - left / config.challengeSeconds), Gold, TelegraphRenderer.Style.Ring, 0.12f, 3f);
            if (left > 0f) return;
            _championUid = -1;
            SfxPlayer.Play2D(SfxId.Boo);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = ChallengeLostText, Color = Danger, Seconds = 1.8f, Kind = AnnounceKind.Whim });
            GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.ChallengeFailed, Whim = WhimKind.ChampionChallenge });
        }

        void ChampionDown(Vector3 at)
        {
            _championUid = -1;
            ChallengesWon++;
            if (Favor.Instance != null) Favor.Instance.Add(config.challengeFavor);
            var pickups = PickupSystem.Instance;
            if (pickups != null)
                for (int c = 0; c < config.challengeGold; c++) pickups.Spawn(PickupSystem.Kind.Gold, at, pickups.config.goldPerCoin);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = ChallengeWonText, Color = Gold, Seconds = 1.8f, Kind = AnnounceKind.Whim });
            GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.ChallengeWon, Whim = WhimKind.ChampionChallenge, Position = at });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.85f);
        }
    }
}
