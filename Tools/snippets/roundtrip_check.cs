// M0.2: instantiate the round-trip FBX and report world-space size / marker directions.
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Models/Dev/SM_RoundTripBox.fbx");
if (go == null) return "FBX not imported";
var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(go);
inst.transform.position = UnityEngine.Vector3.zero;
var sb = new System.Text.StringBuilder();
foreach (var t in inst.GetComponentsInChildren<UnityEngine.Transform>())
{
    var r = t.GetComponent<UnityEngine.MeshRenderer>();
    sb.AppendLine(t.name + " localPos=" + t.localPosition.ToString("F3") + " localRot=" + t.localEulerAngles.ToString("F1") + " localScale=" + t.localScale.ToString("F3")
        + (r ? " worldCenter=" + r.bounds.center.ToString("F3") + " worldSize=" + r.bounds.size.ToString("F3") : ""));
}
var imp = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Art/Models/Dev/SM_RoundTripBox.fbx");
sb.AppendLine("importer: fileScale=" + imp.fileScale + " useFileScale=" + imp.useFileScale + " globalScale=" + imp.globalScale + " materials=" + imp.materialImportMode + " collider=" + imp.addCollider + " readable=" + imp.isReadable + " compression=" + imp.meshCompression);
var box = inst.transform.Find("SM_RoundTripBox") ?? inst.transform;
var mf = box.GetComponent<UnityEngine.MeshFilter>();
if (mf) sb.AppendLine("box mesh: verts=" + mf.sharedMesh.vertexCount + " tris=" + (mf.sharedMesh.triangles.Length / 3) + " localBounds=" + mf.sharedMesh.bounds.size.ToString("F3"));
UnityEngine.Object.DestroyImmediate(inst);
return sb.ToString();
