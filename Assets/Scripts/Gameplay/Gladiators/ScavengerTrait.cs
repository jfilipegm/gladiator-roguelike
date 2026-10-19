using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Pit Goblin's passive (docs/GLADIATORS.md): every `interval` seconds it pulls an improvised weapon out of its
    /// sack: an unlocked pool weapon it doesn't hold, lent beside the slots (WeaponSystem.GrantTemporary, the crowd's
    /// weapon-in-the-sand loan) at level 1 + run level ÷ `runLevelsPerLevel`.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Scavenger", fileName = "TR_Scavenger")]
    public sealed class ScavengerTrait : GladiatorTrait
    {
        [Tooltip("Seconds between improvised weapons (game time).")]
        public float interval = 25f;
        [Tooltip("Seconds into the run before the first one.")]
        public float firstAfter = 12f;
        [Tooltip("Seconds each weapon is lent for.")]
        public float duration = 15f;
        [Tooltip("Lent at level 1 + run level ÷ this…")]
        [Min(1)] public int runLevelsPerLevel = 6;
        [Tooltip("…up to this level.")]
        [Range(1, 8)] public int maxLevel = 6;

        public int LevelFor(int runLevel) => Mathf.Clamp(1 + Mathf.Max(0, runLevel) / Mathf.Max(1, runLevelsPerLevel), 1, maxLevel);

        public override void Install(GameObject player)
        {
            var s = player.GetComponent<Scavenger>();
            if (s == null) s = player.AddComponent<Scavenger>();
            s.trait = this;
        }
    }

    /// <summary>Runtime half of ScavengerTrait: the timer and the pick. Waits while another loan (a crowd gift) is out.</summary>
    public sealed class Scavenger : MonoBehaviour, ITickable
    {
        public ScavengerTrait trait;
        [Tooltip("Junk flying out of the sack (player colours).")]
        public Color junkA = new Color(1f, 0.84f, 0.3f), junkB = new Color(0.85f, 0.95f, 1f);

        WeaponSystem _weapons;
        PlayerBuild _build;
        float _next = -1f;

        public int Fired { get; private set; }
        public WeaponDef LastWeapon { get; private set; }
        /// <summary>Seconds until the next pull.</summary>
        public float NextIn => _next;

        void Awake()
        {
            _weapons = GetComponent<WeaponSystem>();
            _build = GetComponent<PlayerBuild>();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Weapons, -5);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null || dt <= 0f || _weapons == null) return;
            if (_next < 0f) _next = trait.firstAfter;
            _next -= dt;
            if (_next > 0f) return;
            if (_weapons.Temporary != null) { _next = 1f; return; } // a gift loan is out: never cut it short
            _next = Fire() ? trait.interval : 5f;
        }

        /// <summary>Starts the timer over from `firstAfter` (tests).</summary>
        public void ResetTimer() => _next = -1f;

        /// <summary>Pulls a weapon now (tests, admin). False when there's nothing to lend.</summary>
        public bool Fire()
        {
            var def = Pick();
            if (def == null) return false;
            int level = trait.LevelFor(RunState.Current != null ? RunState.Current.Level : 1);
            _weapons.GrantTemporary(def, level, trait.duration);
            Fired++;
            LastWeapon = def;
            SfxPlayer.Play(SfxId.Cackle, transform.position);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(transform.position + Vector3.up * 0.8f, 10, 4f, 0.14f, junkA, junkB);
            return true;
        }

        /// <summary>A random unlocked pool weapon the player doesn't hold (two passes: count, then pick; no allocation).</summary>
        WeaponDef Pick()
        {
            if (_build == null || _build.catalog == null) return null;
            var all = _build.catalog.upgrades;
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (Lendable(all[i])) n++;
            if (n == 0) return null;
            int k = GameRandom.Range(0, n);
            for (int i = 0; i < all.Length; i++)
                if (Lendable(all[i]) && k-- == 0) return all[i].weapon;
            return null;
        }

        bool Lendable(UpgradeDef u) =>
            u != null && u.kind == UpgradeKind.Weapon && u.weapon != null && !_build.State.IsLocked(u) && _weapons.Find(u.weapon) == null;
    }
}
