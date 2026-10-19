using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena's opening beat (M3.5): the game holds paused behind the arena's name while the enemy pools fill and
    /// the first-frame work happens (the ~0.3 s hitch nobody should see), then "FIGHT!" and the run starts. The HUD
    /// draws it from Current; the run clock doesn't move until FIGHT.
    /// </summary>
    public sealed class RunIntro : MonoBehaviour, ITickable
    {
        public enum Stage : byte { Holding, Fight, Done }

        public static RunIntro Instance { get; private set; }

        public string arenaName = "THE SLAVE PIT";
        public string tagline = "Nobody walks out of the pit. Be the first.";
        [Tooltip("Shortest hold on the arena name (real seconds).")]
        public float minHold = 1.2f;
        [Tooltip("How long FIGHT! stays up after the game starts (real seconds).")]
        public float fightBanner = 0.9f;

        bool _paused;

        public Stage Current { get; private set; }
        public float T { get; private set; }
        public bool Done => Current == Stage.Done;
        public string ArenaName => GameSession.ArenaName ?? arenaName;

        void Awake()
        {
            Instance = this;
            TimeController.PushPause(); // in Awake: not a single frame of the run clock before FIGHT
            _paused = true;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 5);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (_paused) { TimeController.PopPause(); _paused = false; }
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (Current == Stage.Done) return;
            T += unscaledDt;
            if (Current == Stage.Holding)
            {
                var d = Director.Instance;
                bool ready = d == null || !d.isActiveAndEnabled || d.Prewarmed;
                if (ready && T >= minHold)
                {
                    if (_paused) { TimeController.PopPause(); _paused = false; }
                    Current = Stage.Fight;
                    T = 0f;
                    SfxPlayer.Play2D(SfxId.Fight);
                    if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.6f);
                }
            }
            else if (T >= fightBanner) Current = Stage.Done;
        }
    }
}
