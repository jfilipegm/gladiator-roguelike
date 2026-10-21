using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Plays every sound effect from a fixed pool of 2D voices (no AudioSource per enemy, no allocation): per-sound repeat
    /// limits, distance fade and stereo pan computed from the camera, priority voice stealing. Combat sounds come from
    /// the event bus (hits, crits, kills, damage, pickups, level-ups, announcer, Director); one-off sounds are played
    /// directly with SfxPlayer.Play / Play2D. Also runs the crowd loop, louder as CrowdExcitement rises. P1 (playtest):
    /// the crowd ducks while the game is paused (level-up cards, pause menu) and comes back on resume; when the run ends
    /// it gives one last reaction (a groan for a death, a cheer for a win) and its loops fade to silence, then stop.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour, ITickable
    {
        public static SfxPlayer Instance { get; private set; }

        public SfxBank bank;
        [Range(4, 48)] public int voices = 24;

        AudioSource[] _src;
        SfxId[] _id;
        int[] _priority;
        float[] _started;
        SfxEntry[] _byId;
        float[] _last;
        AudioSource _crowd, _roar;
        bool _crowdOn, _ending;
        float _murmurLevel, _roarLevel, _duck = 1f, _endT, _endMurmur, _endRoar;

        /// <summary>The crowd layers as they play now (tests, tuning).</summary>
        public float CrowdMurmurVolume => _crowd != null ? _crowd.volume : 0f;
        public float CrowdRoarVolume => _roar != null ? _roar.volume : 0f;
        public float CrowdPitch => _crowd != null ? _crowd.pitch : 1f;
        /// <summary>The crowd's volume factor for pauses (1 = playing, pauseDuck = paused), and whether its loops still
        /// play (false once the end-of-run fade is over). Tests.</summary>
        public float CrowdDuck => _duck;
        public bool CrowdPlaying => _crowd != null && _crowd.isPlaying;
        /// <summary>The run ended: the crowd's last reaction played and its loops are fading out (tests).</summary>
        public bool CrowdEnding => _ending;
        /// <summary>The crowd's last reaction (Boo, slowed into a groan, or Cheer); null until the run ends (tests).</summary>
        public SfxId? CrowdLastWord { get; private set; }
        Transform _cam;

        [Header("Crowd: pauses and the end of the run (P1)")]
        [Tooltip("Crowd volume while the game is paused (level-up cards, pause menu), as a fraction of its live volume.")]
        [Range(0f, 1f)] public float pauseDuck = 0.25f;
        [Tooltip("Real seconds the crowd takes to duck for a pause, and to come back.")]
        public float duckTime = 0.3f;
        [Tooltip("Real seconds the loops hold after the run ends (under the last reaction) before they fade.")]
        public float endHold = 0.35f;
        [Tooltip("Real seconds the murmur and roar take to fade to silence once the run is over.")]
        public float endFade = 1.5f;
        [Tooltip("The last reaction to a death is the boo, slowed down into a groan (playback speed).")]
        [Range(0.5f, 1f)] public float groanPitch = 0.8f;

        [Header("Gem pickup chain")]
        [Tooltip("Pitch added per gem picked up in a row.")]
        public float gemChainStep = 0.025f;
        public float gemChainMaxPitch = 0.6f;
        [Tooltip("Seconds without a gem that reset the chain.")]
        public float gemChainGap = 0.35f;
        int _gemChain;
        float _lastGem = -9f;

        public int Played { get; private set; }

        public static void Play(SfxId id, Vector3 position, float volume = 1f)
        {
            if (Instance != null) Instance.PlayAt(id, position, true, volume);
        }

        public static void Play2D(SfxId id, float volume = 1f)
        {
            if (Instance != null) Instance.PlayAt(id, Vector3.zero, false, volume);
        }

        void Awake()
        {
            Instance = this;
            int n = System.Enum.GetValues(typeof(SfxId)).Length;
            _byId = new SfxEntry[n];
            _last = new float[n];
            for (int k = 0; k < n; k++) _last[k] = -999f;
            if (bank != null)
                foreach (var e in bank.entries) _byId[(int)e.id] = e;
            _src = new AudioSource[voices];
            _id = new SfxId[voices];
            _priority = new int[voices];
            _started = new float[voices];
            for (int v = 0; v < voices; v++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true;
                if (bank != null) s.outputAudioMixerGroup = bank.sfxGroup;
                _src[v] = s;
            }
            if (bank != null && bank.crowdLoop != null)
            {
                _crowd = gameObject.AddComponent<AudioSource>();
                _crowd.clip = bank.crowdLoop;
                _crowd.loop = true;
                _crowd.playOnAwake = false;
                _crowd.spatialBlend = 0f;
                _crowd.volume = _murmurLevel = bank.crowdQuiet;
                _crowd.outputAudioMixerGroup = bank.crowdGroup;
                if (bank.crowdRoar != null)
                {
                    _roar = gameObject.AddComponent<AudioSource>();
                    _roar.clip = bank.crowdRoar;
                    _roar.loop = true;
                    _roar.playOnAwake = false;
                    _roar.spatialBlend = 0f;
                    _roar.volume = 0f;
                    _roar.outputAudioMixerGroup = bank.crowdGroup;
                }
            }
            GameOptions.Apply(bank != null ? bank.mixer : null);
        }

        void Start()
        {
            GameOptions.Apply(bank != null ? bank.mixer : null); // again: a mixer ignores SetFloat until it's running
            if (_crowd != null && CrowdExcitement.Instance != null)
            {
                _crowd.time = Random.value * _crowd.clip.length * 0.9f; // a different stretch of the crowd each run
                _crowd.Play();
                if (_roar != null) { _roar.time = Random.value * _roar.clip.length * 0.9f; _roar.Play(); }
                _crowdOn = true;
            }
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 50);
            GameEvents.OnHit.Subscribe(OnHit);
            GameEvents.OnCrit.Subscribe(OnCrit);
            GameEvents.OnKill.Subscribe(OnKill);
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnPickup.Subscribe(OnPickup);
            GameEvents.OnLevelUp.Subscribe(OnLevelUp);
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
            GameEvents.OnDirector.Subscribe(OnDirector);
            GameEvents.OnRunEnd.Subscribe(OnRunEnd);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnHit.Unsubscribe(OnHit);
            GameEvents.OnCrit.Unsubscribe(OnCrit);
            GameEvents.OnKill.Unsubscribe(OnKill);
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnPickup.Unsubscribe(OnPickup);
            GameEvents.OnLevelUp.Unsubscribe(OnLevelUp);
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            GameEvents.OnDirector.Unsubscribe(OnDirector);
            GameEvents.OnRunEnd.Unsubscribe(OnRunEnd);
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_crowd == null) return;
            if (_ending) { TickEnd(unscaledDt); return; }
            var c = CrowdExcitement.Instance;
            float ex = c != null ? c.Value : 0f, act = c != null ? c.Activity : 0f;
            // the murmur is always there, louder in a fight; the roar rides on top as excitement climbs
            float murmur = c != null ? Mathf.Lerp(bank.crowdQuiet, bank.crowdMurmurFight, act) * Mathf.Lerp(1f, 0.75f, ex) : 0f;
            float roar = c != null ? bank.crowdLoud * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bank.crowdRoarFrom, 1f, ex)) : 0f;
            _murmurLevel = Chase(_murmurLevel, murmur, unscaledDt);
            _roarLevel = Chase(_roarLevel, roar, unscaledDt);
            // paused (cards, pause menu): the crowd drops back behind the menu and swells again on resume
            _duck = Mathf.MoveTowards(_duck, TimeController.IsPaused ? pauseDuck : 1f, unscaledDt * (1f - pauseDuck) / Mathf.Max(0.01f, duckTime));
            _crowd.volume = _murmurLevel * _duck;
            if (_roar != null) _roar.volume = _roarLevel * _duck;
            // nobody fighting: the whole crowd slows and drops a little (a settling murmur); excited: a touch faster
            float pitch = Mathf.Lerp(bank.crowdCalmPitch, 1f, act) + bank.crowdExcitedPitch * ex;
            _crowd.pitch = Mathf.MoveTowards(_crowd.pitch, pitch, unscaledDt * 0.25f);
            if (_roar != null) _roar.pitch = _crowd.pitch;
        }

        float Chase(float v, float target, float dt) => Mathf.MoveTowards(v, target, dt * (target > v ? bank.crowdRise : bank.crowdFall));

        /// <summary>The run is over: the crowd's last word, then its loops fade from where they are to silence.</summary>
        void OnRunEnd(in RunEndEvent e)
        {
            if (_ending || _crowd == null) return;
            _ending = true;
            _endT = 0f;
            _endMurmur = _crowd.volume;
            _endRoar = _roar != null ? _roar.volume : 0f;
            if (!_crowdOn) return; // no crowd in this arena (dev scenes): nothing to react
            CrowdLastWord = e.Outcome == RunOutcome.Died ? SfxId.Boo : SfxId.Cheer;
            PlayAt(CrowdLastWord.Value, Vector3.zero, false, 1f, e.Outcome == RunOutcome.Died ? groanPitch : 1f, force: true); // a groan, or a cheer
        }

        /// <summary>Real time, so the fade runs on while the end screen pauses the game.</summary>
        void TickEnd(float unscaledDt)
        {
            if (!_crowd.isPlaying && (_roar == null || !_roar.isPlaying)) return;
            _endT += unscaledDt;
            float k = Mathf.Clamp01((_endT - endHold) / Mathf.Max(0.01f, endFade));
            float f = (1f - k) * (1f - k); // eases out: most of the noise goes early, the last of it trails off
            _crowd.volume = _endMurmur * f;
            if (_roar != null) _roar.volume = _endRoar * f;
            if (k < 1f) return;
            _crowd.Stop(); // silence, not a loop at volume 0
            if (_roar != null) _roar.Stop();
        }

        // ---------- events ----------
        void OnHit(in HitEvent e)
        {
            if ((e.Flags & DamageFlags.Status) != 0 || e.Crit) return; // ticks are silent; crits have their own sound
            if (e.Blocked) PlayAt(SfxId.Blocked, e.Position, true, 1f);
            else if ((e.Flags & DamageFlags.Proc) != 0) PlayAt(SfxId.Spark, e.Position, true, 1f);
            else PlayAt(SfxId.Hit, e.Position, true, 1f);
        }

        void OnCrit(in HitEvent e) => PlayAt(SfxId.Crit, e.Position, true, 1f);
        void OnKill(in KillEvent e) => PlayAt(e.Elite ? SfxId.EliteKill : SfxId.Kill, e.Position, true, 1f);
        void OnDamaged(in DamagedEvent e) => PlayAt(SfxId.PlayerHurt, e.Position, false, 1f);
        void OnPickup(in PickupEvent e)
        {
            if (e.Kind == (int)PickupSystem.Kind.Gold) { PlayAt(SfxId.Coin, e.Position, false, 1f); return; }
            // a vacuumed stream of gems climbs in pitch (resets after a short gap)
            float now = Time.unscaledTime;
            _gemChain = now - _lastGem < gemChainGap ? _gemChain + 1 : 0;
            _lastGem = now;
            PlayAt(SfxId.Gem, e.Position, false, 1f, 1f + Mathf.Min(gemChainMaxPitch, _gemChain * gemChainStep));
        }
        void OnLevelUp(in LevelUpEvent e) => PlayAt(SfxId.LevelUp, Vector3.zero, false, 1f);
        void OnAnnounce(in AnnounceEvent e) => PlayAt(SfxId.Announce, Vector3.zero, false, 1f);
        void OnDirector(in DirectorEvent e) => PlayAt(e.Kind == DirectorEventKind.Swarm ? SfxId.Horn : SfxId.Announce, e.Position, false, 1f);

        // ---------- voices ----------
        /// <param name="force">Ignore the repeat limits and steal any voice (the crowd's last reaction must be heard).</param>
        void PlayAt(SfxId id, Vector3 position, bool positional, float volume, float pitch = 1f, bool force = false)
        {
            if (_src == null) return;
            var e = _byId[(int)id];
            if (e.clip == null) return;
            float now = Time.unscaledTime;
            if (!force && now - _last[(int)id] < e.minInterval) return;
            float v = e.volume * volume, pan = 0f;
            if (positional && e.positional)
            {
                if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
                var p = Player.Main;
                if (p != null)
                {
                    Vector3 to = position - p.transform.position;
                    float d = to.magnitude;
                    v *= 1f - Mathf.Clamp01((d - bank.fullVolumeDistance) / Mathf.Max(1f, bank.silentDistance - bank.fullVolumeDistance));
                    if (_cam != null && d > 0.5f) pan = Mathf.Clamp(Vector3.Dot(to / d, _cam.right), -1f, 1f) * bank.maxPan;
                }
            }
            if (v < 0.02f) return;
            if (!force && e.maxVoices > 0 && Voices(id) >= e.maxVoices) return;
            int s = PickVoice(force ? int.MaxValue : e.priority);
            if (s < 0) return;
            _last[(int)id] = now;
            var src = _src[s];
            src.Stop();
            src.clip = e.clip;
            src.volume = v;
            src.pitch = pitch != 1f ? pitch : 1f + (e.pitchJitter > 0f ? Random.Range(-e.pitchJitter, e.pitchJitter) : 0f);
            src.panStereo = pan;
            src.outputAudioMixerGroup = e.crowdBus ? bank.crowdGroup : bank.sfxGroup;
            src.Play();
            _id[s] = id;
            _priority[s] = e.priority;
            _started[s] = now;
            Played++;
        }

        int Voices(SfxId id)
        {
            int n = 0;
            for (int s = 0; s < _src.Length; s++) if (_id[s] == id && _src[s].isPlaying) n++;
            return n;
        }

        int PickVoice(int priority)
        {
            int oldest = -1;
            float oldestT = float.MaxValue;
            for (int s = 0; s < _src.Length; s++)
            {
                if (!_src[s].isPlaying) return s;
                if (_priority[s] <= priority && _started[s] < oldestT) { oldestT = _started[s]; oldest = s; }
            }
            return oldest;
        }
    }
}
