using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Murder Machine's passive (docs/GLADIATORS.md): every hit he takes is `flatReduction` lower (after the arena's
    /// multiplier and Armor, never below 1; status ticks aren't reduced) and knockback doesn't move him.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Executioner's Plate", fileName = "TR_ExecutionersPlate")]
    public sealed class ExecutionersPlateTrait : GladiatorTrait
    {
        [Tooltip("HP taken off every hit (never below 1).")]
        public float flatReduction = 4f;
        [Tooltip("Knockback he takes × this (0 = none).")]
        [Range(0f, 1f)] public float knockbackTaken = 0f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<ExecutionersPlate>();
            if (c == null) c = player.AddComponent<ExecutionersPlate>();
            c.trait = this;
            c.Apply();
        }
    }

    /// <summary>Runtime half of ExecutionersPlateTrait: sets PlayerHealth.FlatReduction and PlayerMotor.KnockbackTakenMul.</summary>
    public sealed class ExecutionersPlate : MonoBehaviour
    {
        public ExecutionersPlateTrait trait;

        void OnEnable() => Apply();

        void OnDisable()
        {
            var h = GetComponent<PlayerHealth>();
            if (h != null) h.FlatReduction = 0f;
            var m = GetComponent<PlayerMotor>();
            if (m != null) m.KnockbackTakenMul = 1f;
        }

        public void Apply()
        {
            if (trait == null) return;
            var h = GetComponent<PlayerHealth>();
            if (h != null) h.FlatReduction = trait.flatReduction;
            var m = GetComponent<PlayerMotor>();
            if (m != null) m.KnockbackTakenMul = trait.knockbackTaken;
        }
    }
}
