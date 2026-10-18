using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>Project-wide settings that scenes rely on: layer names and the physics collision matrix.</summary>
    public static class ProjectSetup
    {
        static readonly (int index, string name)[] LayerNames =
        {
            (Layers.Environment, "Environment"), (Layers.Player, "Player"), (Layers.Enemy, "Enemy"),
            (Layers.Pickup, "Pickup"), (Layers.Projectile, "Projectile"), (Layers.Trigger, "Trigger"), (Layers.Props, "Props"),
        };

        [MenuItem("Gladiator/Setup/Project/Ensure Layers And Physics")]
        public static string EnsureLayersAndPhysics()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            foreach (var (index, name) in LayerNames)
                layers.GetArrayElementAtIndex(index).stringValue = name;
            tm.ApplyModifiedPropertiesWithoutUndo();

            // Enemies, pickups and projectiles never use physics collision (spatial hash + central tick instead).
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(Layers.Enemy, i, true);
                Physics.IgnoreLayerCollision(Layers.Pickup, i, true);
                Physics.IgnoreLayerCollision(Layers.Projectile, i, true);
            }
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Environment, false);
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Props, false);
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Trigger, false);
            AssetDatabase.SaveAssets();
            return "layers + physics ok";
        }
    }
}
