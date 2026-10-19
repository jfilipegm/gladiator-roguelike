using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// GameOptions writes the render-scale option into the URP asset at runtime. In the Editor that would change the
    /// project asset for good, so its value is remembered when Play mode starts and put back when it ends.
    /// </summary>
    [InitializeOnLoad]
    static class OptionsEditorGuard
    {
        static float _saved = -1f;

        static OptionsEditorGuard() => EditorApplication.playModeStateChanged += OnPlayMode;

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            if (change == PlayModeStateChange.ExitingEditMode) _saved = urp.renderScale;
            else if (change == PlayModeStateChange.EnteredEditMode && _saved > 0f)
            {
                urp.renderScale = _saved;
                _saved = -1f;
            }
        }
    }
}
