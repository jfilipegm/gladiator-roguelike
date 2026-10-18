// Instantiates the round-trip FBXs and reports world-space positions / sizes / rotations of every node.
var sb = new System.Text.StringBuilder();
foreach (var path in new[] { "Assets/Art/Models/Dev/SM_RoundTripBox.fbx", "Assets/Art/Models/Dev/SM_RoundTripRig.fbx" })
{
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    if (go == null) { sb.AppendLine(path + " NOT IMPORTED"); continue; }
    var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(go);
    inst.transform.position = UnityEngine.Vector3.zero;
    sb.AppendLine("== " + path);
    foreach (var t in inst.GetComponentsInChildren<UnityEngine.Transform>())
    {
        var r = t.GetComponent<UnityEngine.MeshRenderer>();
        sb.AppendLine("  " + t.name + " pos=" + t.position.ToString("F3") + " rot=" + t.rotation.eulerAngles.ToString("F1") + " scale=" + t.lossyScale.ToString("F3")
            + (r ? " meshCenter=" + r.bounds.center.ToString("F4") + " size=" + r.bounds.size.ToString("F3") : ""));
    }
    UnityEngine.Object.DestroyImmediate(inst);
}
return sb.ToString();
