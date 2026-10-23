using Gladiator.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Admin tools (F1 in any arena while AdminMode.On: the Editor, development builds, or a release build switched to
    /// admin mode): % buffs, god mode / timeless, run shortcuts, spawning any enemy type from the EnemyCatalog, freezing
    /// the enemies, fighting any boss (T1), crowd size and game speed. Opening it pauses the game.
    /// The UI (Gladiator.UI.AdminPanel) only draws this state and calls these methods; tests call them directly.
    /// </summary>
    public sealed class AdminController : MonoBehaviour, ITickable
    {
        public enum Buff { Damage, AttackSpeed, Range, MaxHp, MoveSpeed, PickupRange, Crit }
        public const int BuffCount = 7;

        public static AdminController Instance { get; private set; }

        public EnemyCatalog catalog;
        public DebugEnemySpawner crowd;
        public PlayerHealth health;
        public PickupSystem pickups;

        public static readonly int[] SpawnCounts = { 1, 5, 10, 25, 50, 100 };
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 1.5f, 2f };

        public bool IsOpen { get; private set; }
        public int SelectedEnemy { get; private set; }
        public int SpawnCountIndex { get; private set; } = 2;
        public int SpawnCount => SpawnCounts[SpawnCountIndex];
        public EnemyDef Selected => catalog != null && catalog.enemies.Length > 0 ? catalog.enemies[Mathf.Clamp(SelectedEnemy, 0, catalog.enemies.Length - 1)] : null;
        /// <summary>0 = normal enemies, k = catalog.affixes[k-1] (every spawn from the menu is that elite).</summary>
        public int SelectedAffix { get; private set; }
        public AffixDef Affix => catalog != null && SelectedAffix > 0 && SelectedAffix <= catalog.affixes.Length ? catalog.affixes[SelectedAffix - 1] : null;
        /// <summary>F1 opens the panel (T1: admin mode, in any build).</summary>
        public bool Available => AdminMode.On;

        void Awake()
        {
            Instance = this;
            // start the type selector on whatever the background crowd spawns (the catalog is sorted by asset name)
            if (catalog != null && crowd != null && crowd.enemy != null)
                SelectedEnemy = Mathf.Max(0, System.Array.IndexOf(catalog.enemies, crowd.enemy));
        }

        /// <summary>A FIGHT A BOSS load is under way (FightBoss): this arena's run timer jumps to its boss.</summary>
        void Start()
        {
            if (PendingBossFight) TakeBossFight();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Input, -10);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (IsOpen) SetOpen(false);
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt, float unscaledDt)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && Available) SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (open) { TimeController.PushPause(); Player.Main?.BlockInput(); }
            else { TimeController.PopPause(); Player.Main?.UnblockInput(); }
        }

        // ---------- buffs ----------
        public static float GetBuff(Buff b) => b switch
        {
            Buff.Damage => DevSettings.DamagePct,
            Buff.AttackSpeed => DevSettings.AttackSpeedPct,
            Buff.Range => DevSettings.RangePct,
            Buff.MaxHp => DevSettings.MaxHpPct,
            Buff.MoveSpeed => DevSettings.MoveSpeedPct,
            Buff.PickupRange => DevSettings.PickupRangePct,
            _ => DevSettings.CritPct,
        };

        public static float Step(Buff b) => b == Buff.Crit ? 5f : 10f;

        public void SetBuff(Buff b, float pct)
        {
            pct = b == Buff.Crit ? Mathf.Clamp(pct, 0f, 100f) : Mathf.Clamp(pct, -90f, 1000f);
            float oldMax = health != null ? health.MaxHp : 0f;
            switch (b)
            {
                case Buff.Damage: DevSettings.DamagePct = pct; break;
                case Buff.AttackSpeed: DevSettings.AttackSpeedPct = pct; break;
                case Buff.Range: DevSettings.RangePct = pct; break;
                case Buff.MaxHp: DevSettings.MaxHpPct = pct; break;
                case Buff.MoveSpeed: DevSettings.MoveSpeedPct = pct; break;
                case Buff.PickupRange: DevSettings.PickupRangePct = pct; break;
                case Buff.Crit: DevSettings.CritPct = pct; break;
            }
            if (b == Buff.MaxHp && health != null) health.RescaleFrom(oldMax);
        }

        public void AdjustBuff(Buff b, int steps) => SetBuff(b, GetBuff(b) + steps * Step(b));

        public void ResetBuffs()
        {
            float oldMax = health != null ? health.MaxHp : 0f;
            DevSettings.ResetBuffs();
            if (health != null) health.RescaleFrom(oldMax);
        }

        // ---------- run ----------
        public void ToggleGodMode() => DevSettings.GodMode = !DevSettings.GodMode;
        public void ToggleTimeless() => DevSettings.Timeless = !DevSettings.Timeless;

        public void LevelUp()
        {
            var run = RunState.Current;
            if (run != null) run.AddXp(Mathf.Max(0.01f, run.XpToNext - run.Xp));
        }

        public void AddGold(int amount) => RunState.Current?.AddGold(amount);
        public void AddFavor(float amount) { if (Favor.Instance != null) Favor.Instance.Add(amount); }
        public void GiftNow() { if (CrowdGifts.Instance != null) CrowdGifts.Instance.Throw(); }
        public void WhimNow() { if (ArenaMaster.Instance != null) ArenaMaster.Instance.FireWhim(); }
        public void Heal() => health?.HealFull();
        public void KillPlayer() { SetOpen(false); health?.Kill(); }
        public void SetTimer(float secondsLeft) => RunState.Current?.SetTimeRemaining(secondsLeft);
        public void AddTime(float seconds) { var r = RunState.Current; if (r != null) r.SetTimeRemaining(r.TimeRemaining + seconds); }

        public void Restart()
        {
            SetOpen(false);
            if (RunEndController.Instance != null) RunEndController.Instance.Retry();
        }

        // ---------- enemies ----------
        public void CycleEnemy(int dir)
        {
            int n = catalog != null ? catalog.enemies.Length : 0;
            if (n == 0) return;
            SelectedEnemy = ((SelectedEnemy + dir) % n + n) % n;
            if (crowd != null && Selected != null) crowd.enemy = Selected; // the background crowd follows the selection
        }

        // ---------- upgrades ----------
        public int SelectedUpgrade { get; private set; }
        public Rarity SelectedRarity { get; private set; }
        public PlayerBuild Build => Player.Main != null ? Player.Main.GetComponent<PlayerBuild>() : null;

        public UpgradeDef Upgrade
        {
            get
            {
                var b = Build;
                if (b == null || b.catalog == null || b.catalog.upgrades.Length == 0) return null;
                return b.catalog.upgrades[Mathf.Clamp(SelectedUpgrade, 0, b.catalog.upgrades.Length - 1)];
            }
        }

        public void CycleUpgrade(int dir)
        {
            var b = Build;
            int n = b != null && b.catalog != null ? b.catalog.upgrades.Length : 0;
            if (n > 0) SelectedUpgrade = ((SelectedUpgrade + dir) % n + n) % n;
        }

        public void CycleRarity() => SelectedRarity = (Rarity)(((int)SelectedRarity + 1) % UpgradeConfig.RarityCount);

        /// <summary>Takes one level of the selected upgrade at the selected rarity (ignores slots; respects max level).</summary>
        public bool GiveUpgrade()
        {
            var b = Build;
            var d = Upgrade;
            if (b == null || d == null) return false;
            int level = b.State.LevelOf(d);
            if (level >= d.maxLevel) return false;
            b.Take(new CardOffer { Def = d, Rarity = SelectedRarity, Level = level + 1 });
            return true;
        }

        public void CycleAffix(int dir)
        {
            int n = (catalog != null ? catalog.affixes.Length : 0) + 1;
            SelectedAffix = ((SelectedAffix + dir) % n + n) % n;
        }

        public void CycleCount(int dir) => SpawnCountIndex = Mathf.Clamp(SpawnCountIndex + dir, 0, SpawnCounts.Length - 1);

        /// <summary>Spawns `count` of the selected type 3.5–7 m around the player.</summary>
        public int SpawnNearPlayer(int count) => Spawn(count, true);

        /// <summary>Spawns `count` of the selected type spread around the arena, away from the player.</summary>
        public int SpawnAroundArena(int count) => Spawn(count, false);

        int Spawn(int count, bool near)
        {
            var em = EnemyManager.Instance;
            var def = Selected;
            if (em == null || def == null) return 0;
            Vector3 player = Player.Main != null ? Player.Main.transform.position : em.arenaCenter;
            int spawned = 0;
            for (int k = 0; k < count; k++)
            {
                Vector3 p;
                if (near)
                {
                    float a = GameRandom.Range(0f, Mathf.PI * 2f), r = GameRandom.Range(3.5f, 7f);
                    p = player + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                else
                {
                    p = em.arenaCenter;
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        float a = GameRandom.Range(0f, Mathf.PI * 2f);
                        p = em.arenaCenter + (em.EdgePoint(a, 1f) - em.arenaCenter) * Mathf.Sqrt(GameRandom.Range(0.2f, 1f));
                        if ((p - player).sqrMagnitude > 64f) break;
                    }
                }
                em.ClampInside(ref p, 1f);
                if (em.Spawn(def, p, Affix) >= 0) spawned++;
            }
            return spawned;
        }

        public void KillAllEnemies() => EnemyManager.Instance?.KillAll();

        /// <summary>FREEZE: enemies (bosses too) stand still and don't attack, shoot or touch-damage; they still take hits,
        /// flash and die (DevSettings.FreezeEnemies). Turning it on also drops the enemy shots already in the air.</summary>
        public void ToggleFreeze()
        {
            DevSettings.FreezeEnemies = !DevSettings.FreezeEnemies;
            if (DevSettings.FreezeEnemies && EnemyProjectiles.Instance != null) EnemyProjectiles.Instance.Clear();
        }

        public bool DirectorOn => Director.Instance != null && Director.Instance.enabled;
        public void ToggleDirector()
        {
            var d = Director.Instance != null ? Director.Instance : FindAnyObjectByType<Director>(FindObjectsInactive.Include);
            if (d != null) d.enabled = !d.enabled;
        }
        public void SwarmNow() { if (Director.Instance != null) Director.Instance.TriggerSwarmNow(); }
        public void EliteNow() { if (Director.Instance != null) Director.Instance.TriggerEliteNow(); }
        public void ClearPickups() => pickups?.ClearAll();

        public int CrowdSize => crowd != null ? crowd.targetAlive : 0;
        public void AdjustCrowd(int delta) { if (crowd != null) crowd.targetAlive = Mathf.Clamp(crowd.targetAlive + delta, 0, 300); }

        // ---------- fight a boss (T1) ----------

        /// <summary>
        /// Set by FightBoss before the boss's arena loads; that arena's AdminController takes it on Start and jumps the run
        /// timer to BossJumpSeconds. Static: it survives the scene load.
        /// </summary>
        public static bool PendingBossFight;
        /// <summary>Run clock left after a boss jump. The clock waits for the arena intro's FIGHT!, so the boss's entrance
        /// starts this long after it.</summary>
        public const float BossJumpSeconds = 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PendingBossFight = false;

        /// <summary>The menu's runs (GameSession.KnownRuns): one FIGHT A BOSS button each. 0 when no menu ran (an arena
        /// started straight from the Editor).</summary>
        public static int BossRunCount => GameSession.KnownRuns != null ? GameSession.KnownRuns.Length : 0;

        public static RunDef BossRun(int k) => k >= 0 && k < BossRunCount ? GameSession.KnownRuns[k] : null;

        /// <summary>Can run k be fought from here: it exists, has an arena to load and a boss at the end of it.</summary>
        public static bool CanFightBoss(int k)
        {
            var run = BossRun(k);
            return run != null && run.arenas.Length > 0 && !string.IsNullOrEmpty(run.arenas[0].scenePath) && run.Boss != null;
        }

        /// <summary>
        /// FIGHT A BOSS: starts run k of GameSession.KnownRuns as the gladiator and skin you have on now (a normal run, a
        /// fresh seed); its arena then brings the boss in BossJumpSeconds after FIGHT! (TakeBossFight). God mode and the
        /// buffs stay as they are (DevSettings survives the load). False when run k can't be fought.
        /// </summary>
        public bool FightBoss(int k)
        {
            if (!CanFightBoss(k)) return false;
            var run = BossRun(k);
            var loadout = Player.Main != null ? Player.Main.GetComponent<GladiatorLoadout>() : null;
            var gladiator = loadout != null && loadout.Current != null ? loadout.Current : GameSession.Gladiator;
            if (loadout != null) GameSession.Skin = loadout.CurrentSkin; // what you wear now (null = the default look)
            SetOpen(false);
            PendingBossFight = true;
            Debug.Log("[Admin] fight a boss: " + run.id + (gladiator != null ? " as " + gladiator.id : ""));
            GameSession.StartRun(gladiator, run);
            return true;
        }

        /// <summary>
        /// The arena side of FightBoss (Start): the run timer jumps to BossJumpSeconds, and BossFight brings the boss in
        /// when it runs out, the real fight (entrance, phases, hazards). Timeless is switched off: with it the clock never
        /// runs out. The flag is spent here even in an arena without a boss.
        /// </summary>
        void TakeBossFight()
        {
            PendingBossFight = false;
            var fight = FindAnyObjectByType<BossFight>();
            if (fight == null || fight.boss == null || RunState.Current == null)
            {
                Debug.LogWarning("[Admin] fight a boss: this arena has no boss fight");
                return;
            }
            DevSettings.Timeless = false;
            SetTimer(BossJumpSeconds);
            Debug.Log("[Admin] fight a boss: " + fight.boss.id + " in " + BossJumpSeconds + " s");
        }

        // ---------- game speed ----------
        public int SpeedIndex
        {
            get
            {
                int best = 2; float bestD = float.MaxValue;
                for (int i = 0; i < Speeds.Length; i++) { float d = Mathf.Abs(Speeds[i] - TimeController.GameSpeed); if (d < bestD) { bestD = d; best = i; } }
                return best;
            }
        }

        public void CycleSpeed(int dir) => TimeController.GameSpeed = Speeds[Mathf.Clamp(SpeedIndex + dir, 0, Speeds.Length - 1)];
    }
}
