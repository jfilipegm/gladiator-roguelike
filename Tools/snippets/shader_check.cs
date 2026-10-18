// Reports compile messages for every shader under Assets/Art/Shaders.
var sb = new System.Text.StringBuilder();
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Art/Shaders" }))
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var sh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(path);
    UnityEditor.ShaderUtil.CompilePass(new UnityEngine.Material(sh), 0, true);
    var msgs = UnityEditor.ShaderUtil.GetShaderMessages(sh);
    sb.AppendLine(sh.name + " supported=" + sh.isSupported + " passes=" + sh.passCount + " messages=" + msgs.Length);
    foreach (var m in msgs) sb.AppendLine("  " + m.severity + " " + m.message + " (" + m.file + ":" + m.line + ") " + m.platform);
}
return sb.ToString();
