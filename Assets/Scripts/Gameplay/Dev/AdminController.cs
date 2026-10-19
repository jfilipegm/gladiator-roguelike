using Gladiator.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Test-arena admin tools (F1 in the Editor and development builds): % buffs, god mode / timeless, run shortcuts,
    /// spawning any enemy type from the EnemyCatalog, crowd size and game speed. Opening it pauses the game.
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
        [Tooltip("Allow F1 outside development builds (keep off for release).")]
        public bool allowInReleaseBuilds = false;

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
        public bool Available => allowInReleaseBuilds || Debug.isDebugBuild;

        void Awake()
        {
            Instance = this;
            // start the type selector on whatever the background crowd spawns (the catalog is sorted by asset name)
            if (catalog != null && crowd != null && crowd.enemy != null)
                SelectedEnemy = Mathf.Max(0, System.Array.IndexOf(catalog.enemies, crowd.enemy));
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
            float maxR = em.arenaRadius - 1f;
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
                        float a = GameRandom.Range(0f, Mathf.PI * 2f), r = maxR * Mathf.Sqrt(GameRandom.Range(0.2f, 1f));
                        p = em.arenaCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        if ((p - player).sqrMagnitude > 64f) break;
                    }
                }
                float dx = p.x - em.arenaCenter.x, dz = p.z - em.arenaCenter.z, d2 = dx * dx + dz * dz;
                if (d2 > maxR * maxR) { float s = maxR / Mathf.Sqrt(d2); p.x = em.arenaCenter.x + dx * s; p.z = em.arenaCenter.z + dz * s; }
                if (em.Spawn(def, p, Affix) >= 0) spawned++;
            }
            return spawned;
        }

        public void KillAllEnemies() => EnemyManager.Instance?.KillAll();

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
