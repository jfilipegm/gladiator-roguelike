using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Note 30 (v0.29.0): each arena its own Roman chest (ChestSetup), never the pirate one, never its neighbour's.</summary>
    public class ArenaChestTests
    {
        // the ladder's order and each arena's design, as Gladiator/Setup/Arenas/Assign Armory Chests (ChestSetup) sets them
        static readonly (string layout, char variant)[] Arenas =
        {
            ("SlavePit", 'B'), ("ProvincialPit", 'C'), ("StoneAmphitheater", 'A'), ("GrandArena", 'D'), ("ImperialArena", 'C'),
            ("Colosseum", 'A'), ("Playground", 'A'),
        };

        [Test]
        public void EveryArena_HasItsOwnChest_WithALid_AndNeighboursDiffer()
        {
            GameObject last = null;
            foreach (var (layoutName, variant) in Arenas)
            {
                var layout = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>("Assets/Data/Arenas/AL_" + layoutName + ".asset");
                Assert.IsNotNull(layout, layoutName);
                Assert.IsNotNull(layout.chestModel, layoutName + ": Gladiator/Setup/Arenas/Assign Armory Chests");
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/A1/Props/PR_ArmoryChest_" + variant + ".fbx"), layout.chestModel, layoutName + " gets chest " + variant);
                Assert.IsNotNull(Find(layout.chestModel.transform, "Lid"), layoutName + ": the chest opens (a Lid on its hinge)");
                Assert.IsNotNull(Find(layout.chestModel.transform, "Body"), layoutName);
                if (layoutName != "Playground") Assert.AreNotSame(last, layout.chestModel, layoutName + " differs from the arena before it");
                last = layout.chestModel;
            }
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
