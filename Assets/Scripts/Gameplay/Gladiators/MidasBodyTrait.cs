using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The Golden Idol's first passive (docs/GLADIATORS.md): damage from the gold he's carrying.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Midas Body", fileName = "TR_MidasBody")]
    public sealed class MidasBodyTrait : GladiatorTrait
    {
        [Tooltip("Gold per +1 % damage.")]
        public float goldPerPercent = 10f;
        [Tooltip("Cap on the bonus (1 = +100 %).")]
        public float maxBonus = 1f;

        public float BonusAt(int gold) => Mathf.Min(maxBonus, Mathf.Floor(Mathf.Max(0, gold) / Mathf.Max(1f, goldPerPercent)) * 0.01f);

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<MidasBody>();
            if (c == null) c = player.AddComponent<MidasBody>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of MidasBodyTrait: a Damage StatMod that follows RunState.Gold. The HUD reads Instance.</summary>
    public sealed class MidasBody : MonoBehaviour, ITickable
    {
        public static MidasBody Instance { get; private set; }

        public MidasBodyTrait trait;
        PlayerStats _stats;
        StatMod _applied;
        bool _has;

        public float Bonus { get; private set; }
        public float Bonus01 => trait != null && trait.maxBonus > 0f ? Mathf.Clamp01(Bonus / trait.maxBonus) : 0f;

        void Awake() => _stats = GetComponent<PlayerStats>();

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 60);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            Apply(0f);
        }

        public void Tick(float dt, float unscaledDt) => Refresh();

        /// <summary>Re-reads the gold now (tests; the tick does it every frame).</summary>
        public void Refresh()
        {
            var run = RunState.Current;
            Apply(trait == null || run == null ? 0f : trait.BonusAt(run.Gold));
        }

        void Apply(float bonus)
        {
            if (_stats == null || (bonus == Bonus && _has == bonus > 0f)) return;
            if (_has) _stats.Block.Remove(_applied);
            _has = bonus > 0f;
            _applied = new StatMod(StatId.Damage, ModOp.Multiply, 1f + bonus);
            if (_has) _stats.Block.Add(_applied);
            Bonus = bonus;
        }
    }
}
