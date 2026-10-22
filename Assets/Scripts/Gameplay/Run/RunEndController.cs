using Gladiator.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// How a run ends. Player death, the timer reaching 0:00 in an arena without a boss (unless DevSettings.Timeless),
    /// or the boss falling ends the run → the run is recorded in the save (Renown × its edicts, records, unlocks:
    /// MetaProgress) → a short beat (death: slow motion + collapse; victory: every enemy pops) → the game pauses and the
    /// end screen (UI reads ScreenShown/Outcome/Rewards) offers Retry / Main Menu. From the moment the player goes down,
    /// nothing of theirs fights on (RunState.CombatOver); the crowd gives one last reaction and falls silent (SfxPlayer).
    /// </summary>
    public sealed class RunEndController : MonoBehaviour, ITickable
    {
        public static RunEndController Instance { get; private set; }

        public RunState run;
        public PlayerHealth health;
        public PlayerPartAnimator animator;
        public DeathPopFx burst;
        [Tooltip("Meta progression (Renown, unlocks). None = this scene's runs aren't recorded.")]
        public MetaConfig meta;
        [Tooltip("Real-time seconds of the death beat before the end screen.")]
        public float deathBeat = 1.6f;
        [Range(0.05f, 1f)] public float deathSlowMotion = 0.3f;
        [Tooltip("Real-time seconds between the timer reaching 0:00 and the end screen.")]
        public float survivedBeat = 1.2f;

        public enum Phase { Playing, Beat, Screen }
        public Phase CurrentPhase { get; private set; }
        public bool ScreenShown => CurrentPhase == Phase.Screen;
        public RunOutcome Outcome => run != null ? run.Outcome : RunOutcome.None;
        /// <summary>What this run earned (null when it isn't recorded).</summary>
        public RunRewards Rewards { get; private set; }

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
            if (meta != null && run != null) Rewards = MetaProgress.CommitRun(Summary(e), meta);
            if (Player.Main != null && !_blockedInput) { Player.Main.BlockInput(); _blockedInput = true; }
            Vector3 at = health != null ? health.transform.position + Vector3.up * 0.9f : Vector3.zero;
            // P1: the player's side stops at once, whatever the outcome (the weapons, traits and projectiles also check
            // RunState.CombatOver themselves, from the frame the player went down).
            if (PlayerProjectiles.Instance != null) PlayerProjectiles.Instance.Clear();
            var boons = Player.Main != null ? Player.Main.GetComponent<BoonEffects>() : null;
            if (boons != null) boons.Clear(); // no queued bursts or trails (and none from the corpses KillAll makes)
            if (e.Outcome == RunOutcome.Died)
            {
                _beatLeft = deathBeat;
                TimeController.SlowMotion = deathSlowMotion;
                SfxPlayer.Play2D(SfxId.Death);
                if (animator != null) animator.Dead = true;
                if (burst != null) burst.Burst(at, 36, 6f, 0.22f, new Color(1f, 0.33f, 0.2f), new Color(0.35f, 0.05f, 0.08f));
            }
            else
            {
                _beatLeft = survivedBeat;
                SfxPlayer.Play2D(SfxId.Victory);
                if (EnemyManager.Instance != null) EnemyManager.Instance.KillAll(); // the arena falls silent
                if (EnemyProjectiles.Instance != null) EnemyProjectiles.Instance.Clear();
                if (HazardZones.Instance != null) HazardZones.Instance.Clear();
                if (burst != null) burst.Burst(at, 40, 8f, 0.22f, new Color(1f, 0.86f, 0.4f), new Color(0.43f, 0.95f, 1f));
            }
        }

        RunSummary Summary(in RunEndEvent e)
        {
            var s = new RunSummary
            {
                Outcome = e.Outcome, Elapsed = e.Elapsed, Kills = run.Kills, GoldEarned = run.GoldEarned, Level = run.Level,
                ArenaIndex = run.arenaTier, BossDefeated = run.BossDefeated, BossDefeatedHp01 = run.BossDefeatedHp01,
            };
            s.Edicts = RunModifiers.ActiveIds(); // R7-STUB(a)
            s.EdictBonus = RunModifiers.RenownBonus; // R7-STUB(a)
            var p = Player.Main;
            if (p == null) return s;
            var stats = p.GetComponent<PlayerStats>();
            if (stats != null && stats.gladiator != null) s.GladiatorId = stats.gladiator.id;
            var ws = p.GetComponent<WeaponSystem>();
            if (ws != null)
            {
                s.Weapons = new WeaponKills[ws.Weapons.Count];
                for (int k = 0; k < s.Weapons.Length; k++)
                {
                    var def = ws.Weapons[k].Def;
                    s.Weapons[k] = new WeaponKills { Id = def.id, Kills = run.KillsBy(def.RuntimeId) };
                }
            }
            s.EnemyKills = EnemyKills();
            var build = p.GetComponent<PlayerBuild>();
            if (build != null) Discoveries(build, ref s);
            return s;
        }

        IdCount[] EnemyKills()
        {
            var em = EnemyManager.Instance;
            var list = new System.Collections.Generic.List<IdCount>(run.KilledTypeCount);
            for (int k = 0; k < run.KilledTypeCount; k++)
            {
                run.GetTypeKills(k, out int typeId, out int kills);
                var def = em != null ? em.DefByRuntimeId(typeId) : null;
                if (def != null && kills > 0) list.Add(new IdCount { id = def.id, count = kills });
            }
            return list.ToArray();
        }

        /// <summary>What the build found this run: upgrades (an evolution's base card too), evolutions, synergies.</summary>
        static void Discoveries(PlayerBuild build, ref RunSummary s)
        {
            var st = build.State;
            var ups = new System.Collections.Generic.List<string>(st.OwnedCount + 4);
            var evos = new System.Collections.Generic.List<string>(4);
            for (int k = 0; k < st.OwnedCount; k++)
            {
                var d = st.GetOwned(k).Def;
                if (d == null) continue;
                ups.Add(d.id);
                if (!d.IsEvolution) continue;
                evos.Add(d.id);
                if (d.evolvesFrom != null && !ups.Contains(d.evolvesFrom.id)) ups.Add(d.evolvesFrom.id);
            }
            var syn = new string[st.SynergyCount];
            for (int k = 0; k < syn.Length; k++) syn[k] = st.GetSynergy(k).id;
            s.Upgrades = ups.ToArray();
            s.Evolutions = evos.ToArray();
            s.Synergies = syn;
        }

        void ShowScreen()
        {
            CurrentPhase = Phase.Screen;
            TimeController.SlowMotion = 1f;
            if (!_paused) { TimeController.PushPause(); _paused = true; }
        }

        /// <summary>Restart the same arena with a fresh run (dev settings like God mode are kept).</summary>
        public void Retry() => GameSession.Retry();

        /// <summary>Back to the main menu (quits a bare arena build that has no menu).</summary>
        public void Quit() => GameSession.ToMenu();
    }
}
