using UnityEngine;

namespace Gladiator.Gameplay
{
    // R6-STUB(b): a stand-in for R6-B's SkinDef (docs/tracks/R7-contract.md, Wave 3) so R6-C compiles alone.
    // At merge the orchestrator keeps R6-B's file (docs/tracks/R6-menu.md, "Stubs").
    public sealed class SkinDef : ScriptableObject
    {
        public string id, displayName; [TextArea] public string description; public GladiatorDef gladiator; public GameObject model;
        public Sprite portrait; [Tooltip("Empty = free.")] public UnlockDef unlock; public Color[] chips = new Color[3];
    }
}
