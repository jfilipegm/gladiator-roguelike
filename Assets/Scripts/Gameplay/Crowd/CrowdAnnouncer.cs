using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena's voice: kill-streak lines, the close-call line and the crowd's roar, raised as AnnounceEvents (HUD
    /// text + stinger) with crowd spikes. Kill times live in a fixed ring buffer (no allocation). Silent while a boss
    /// fights (his beats own the announcer).
    /// </summary>
    public sealed class CrowdAnnouncer : MonoBehaviour, ITickable
    {
        public AnnouncerConfig config;

        const int Ring = 512;
        readonly float[] _kills = new float[Ring];
        int _head, _count;
        float _clock, _lastLine = -999f, _lastStreak = -999f, _lastClose = -999f, _lastRoar = -999f;
        int _streakTier = -1;
        bool _wasLow;
        PlayerHealth _health;

        public int Lines { get; private set; }
        public int KillsInWindow => CountRecent();

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 6);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
        }

        void OnKill(in KillEvent e)
        {
            _kills[_head] = _clock;
            _head = (_head + 1) % Ring;
            if (_count < Ring) _count++;
        }

        int CountRecent()
        {
            if (config == null) return 0;
            int n = 0;
            for (int k = 1; k <= _count; k++)
            {
                if (_clock - _kills[(_head - k + Ring) % Ring] > config.window) break;
                n++;
            }
            return n;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (config == null || dt <= 0f) return;
            _clock += dt;
            var run = RunState.Current;
            if (run != null && run.Ended) return;
            var boss = BossController.Active;
            if (boss != null && !boss.Defeated) return;
            if (_clock - _lastStreak > config.streakCooldown) _streakTier = -1;
            bool gap = _clock - _lastLine >= config.minGap;

            int recent = CountRecent();
            for (int t = config.streaks.Length - 1; t > _streakTier; t--)
            {
                if (recent < config.streaks[t].kills) continue;
                if (gap) { Say(config.streaks[t].text, config.streaks[t].crowd); _streakTier = t; _lastStreak = _clock; }
                return;
            }

            if (_health == null && Player.Main != null) _health = Player.Main.GetComponent<PlayerHealth>();
            if (_health != null && !_health.IsDead)
            {
                if (_health.Hp01 < config.closeCallLow) _wasLow = true;
                else if (_wasLow && _health.Hp01 >= config.closeCallRecovered)
                {
                    _wasLow = false;
                    if (gap && _clock - _lastClose > config.closeCallCooldown) { Say(config.closeCallText, 0.8f); _lastClose = _clock; return; }
                }
            }

            var crowd = CrowdExcitement.Instance;
            if (gap && crowd != null && crowd.Value >= config.roarAt && _clock - _lastRoar > config.roarCooldown)
            {
                Say(config.roarText, 1f);
                _lastRoar = _clock;
            }
        }

        void Say(string text, float crowd)
        {
            _lastLine = _clock;
            Lines++;
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = text, Color = config.color, Seconds = config.seconds });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(crowd);
        }
    }
}
