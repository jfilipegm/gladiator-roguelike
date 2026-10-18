using UnityEditor;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Enforces import settings for procedural Blender exports under Assets/Art/Models (CLAUDE.md §3.3):
    /// scale factor 1, no embedded materials (shared palette materials are assigned by setup scripts),
    /// no colliders, no animation/cameras/lights, mesh compression on, read/write off.
    /// </summary>
    public class ModelImportRules : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/Art/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var mi = (ModelImporter)assetImporter;

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.addCollider = false;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.isReadable = assetPath.Contains("/ReadWrite/"); // opt-in folder for meshes the code must read
            mi.importNormals = ModelImporterNormals.Import;     // flat normals authored in Blender
            mi.importTangents = ModelImporterTangents.None;     // no normal maps in this style
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = false;                            // keep hard edges / palette UV seams
            mi.preserveHierarchy = true;                        // rigid-part characters keep their joints
        }
    }
}
