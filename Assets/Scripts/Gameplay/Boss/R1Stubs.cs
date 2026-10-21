// ============================================================================================================
// R1 STUBS — DELETE THIS FILE WHEN THE CHARIOT-RACES AND FORMATIONS PARTS ARE MERGED (docs/tracks/R1-boss.md).
// Only so the boss part compiles on its own (Tools/ccheck): the names below are the ones docs/tracks/R1-design.md
// gives those parts, and EmperorsChampionTests calls them. Once the real ChariotRace.cs and the formation code in
// EnemyManager.Leash.cs exist, this file is a duplicate definition and won't compile: delete it.
// ============================================================================================================
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>STUB (R1, chariot races part): the public surface EmperorsChampionTests reads.</summary>
    public sealed class ChariotRace : MonoBehaviour
    {
        public static ChariotRace Instance { get; private set; }
        public bool Running { get; private set; }
        public bool Telegraphing { get; private set; }
        public bool BackToBack { get; set; }
        public int Races { get; private set; }
        public int BossHits { get; private set; }
        public void StartNow() { }
        public void Halt() { }
    }

    /// <summary>STUB (R1, formations part): the escorts still in a leader's line.</summary>
    public sealed partial class EnemyManager
    {
        public int FormedCount(int leader) => 0;
    }
}
