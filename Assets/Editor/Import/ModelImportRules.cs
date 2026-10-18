using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
            mi.bakeAxisConversion = true;   // Blender Z-up -> Unity Y-up baked into meshes AND node transforms (works for empties too)
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

        /// <summary>
        /// gladiator_kit.export_fbx gives each FBX root node a 180° yaw (so assets face +Z). Bake it into the subtree
        /// so every node arrives with identity rotation and part-local +Z == character forward:
        /// for all descendants p' = q·p, r' = q·r·q⁻¹, and every mesh's vertices/normals v' = q·v.
        /// </summary>
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            foreach (Transform node in root.transform)
            {
                var q = node.localRotation;
                if (Quaternion.Angle(q, Quaternion.identity) < 0.01f) continue;
                var done = new HashSet<Mesh>();
                RotateMeshes(node, q, done);
                foreach (Transform child in node) Conjugate(child, q, done);
                node.localRotation = Quaternion.identity;
            }
        }

        static void Conjugate(Transform t, Quaternion q, HashSet<Mesh> done)
        {
            t.localPosition = q * t.localPosition;
            t.localRotation = q * t.localRotation * Quaternion.Inverse(q);
            RotateMeshes(t, q, done);
            foreach (Transform child in t) Conjugate(child, q, done);
        }

        static void RotateMeshes(Transform t, Quaternion q, HashSet<Mesh> done)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !done.Add(mf.sharedMesh)) return;
            var mesh = mf.sharedMesh;
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i++) v[i] = q * v[i];
            for (int i = 0; i < n.Length; i++) n[i] = q * n[i];
            mesh.vertices = v;
            if (n.Length == v.Length) mesh.normals = n;
            mesh.RecalculateBounds();
        }
    }
}
