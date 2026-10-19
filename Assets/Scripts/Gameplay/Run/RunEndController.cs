using Gladiator.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// How a run ends (slice placeholder for M3.5's full flow). Player death or the timer reaching 0:00 (unless
    /// DevSettings.Timeless) ends the run → a short beat (death: slow motion + collapse; survived: every enemy
    /// pops) → the game pauses and the end screen (UI reads ScreenShown/Outcome) offers Retry / Quit.
    /// Once Gorm exists, the timer summons him instead and "Survived" moves to after the boss.
    /// </summary>
    public sealed class RunEndController : MonoBehaviour, ITickable
    {
        public static RunEndController Instance { get; private set; }

        public RunState run;
        public PlayerHealth health;
        public PlayerPartAnimator animator;
        public DeathPopFx burst;
        [Tooltip("Real-time seconds of the death beat before the end screen.")]
        public float deathBeat = 1.6f;
        [Range(0.05f, 1f)] public float deathSlowMotion = 0.3f;
        [Tooltip("Real-time seconds between the timer reaching 0:00 and the end screen.")]
        public float survivedBeat = 1.2f;

        public enum Phase { Playing, Beat, Screen }
        public Phase CurrentPhase { get; private set; }
        public bool ScreenShown => CurrentPhase == Phase.Screen;
        public RunOutcome Outcome => run != null ? run.Outcome : RunOutcome.None;

        float _beatLeft;
        bool _blockedInput, _paused;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late, 5);
            GameEvents.OnRunEnd.Subscribe(OnRunEnd);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnRunEnd.Unsubscribe(OnRunEnd);
            // Scene unloading (Retry, tests, menus later): never leave the static time state paused or slowed.
            if (_paused) { TimeController.PopPause(); _paused = false; }
            if (CurrentPhase == Phase.Beat) TimeController.SlowMotion = 1f;
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (CurrentPhase == Phase.Playing)
            {
                if (health != null && health.IsDead && run != null) run.End(RunOutcome.Died);
                return;
            }
            if (CurrentPhase == Phase.Beat)
            {
                _beatLeft -= unscaledDt;
                if (_beatLeft <= 0f) ShowScreen();
            }
        }

        void OnRunEnd(in RunEndEvent e)
        {
            if (CurrentPhase != Phase.Playing) return;
            CurrentPhase = Phase.Beat;
            if (Player.Main != null && !_blockedInput) { Player.Main.BlockInput(); _blockedInput = true; }
            Vector3 at = health != null ? health.transform.position + Vector3.up * 0.9f : Vector3.zero;
            if (e.Outcome == RunOutcome.Died)
            {
                _beatLeft = deathBeat;
                TimeController.SlowMotion = deathSlowMotion;
                if (animator != null) animator.Dead = true;
                if (burst != null) burst.Burst(at, 36, 6f, 0.22f, new Color(1f, 0.33f, 0.2f), new Color(0.35f, 0.05f, 0.08f));
            }
            else
            {
                _beatLeft = survivedBeat;
                if (EnemyManager.Instance != null) EnemyManager.Instance.KillAll(); // the arena falls silent
                if (EnemyProjectiles.Instance != null) EnemyProjectiles.Instance.Clear();
                if (HazardZones.Instance != null) HazardZones.Instance.Clear();
                if (PlayerProjectiles.Instance != null) PlayerProjectiles.Instance.Clear();
                var boons = Player.Main != null ? Player.Main.GetComponent<BoonEffects>() : null;
                if (boons != null) boons.Clear(); // no bursts from the corpses KillAll just made
                if (burst != null) burst.Burst(at, 40, 8f, 0.22f, new Color(1f, 0.86f, 0.4f), new Color(0.43f, 0.95f, 1f));
            }
        }

        void ShowScreen()
        {
            CurrentPhase = Phase.Screen;
            TimeController.SlowMotion = 1f;
            if (!_paused) { TimeController.PushPause(); _paused = true; }
        }

        /// <summary>Restart the same arena with a fresh run (dev settings like God mode are kept).</summary>
        public void Retry()
        {
            TimeController.ResetForNewRun();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.path);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
