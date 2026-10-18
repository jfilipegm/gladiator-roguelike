using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Small helpers meant to be called from the live-Editor bridge (Tools/ueval), e.g.
    /// <c>return Gladiator.EditorTools.EditorCli.RenderCamera("Main Camera", "Screenshots/x.png", 1280, 720);</c>
    /// </summary>
    public static class EditorCli
    {
        /// <summary>Renders a camera (by GameObject name, or Camera.main when empty) into a PNG under the project root.</summary>
        public static string RenderCamera(string cameraName, string relativePath, int width = 1280, int height = 720)
        {
            Camera cam = null;
            if (string.IsNullOrEmpty(cameraName)) cam = Camera.main;
            else
            {
                var go = GameObject.Find(cameraName);
                if (go != null) cam = go.GetComponent<Camera>();
            }
            if (cam == null) return "ERROR: camera not found: " + cameraName;

            var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            return path;
        }

        /// <summary>Error / warning / log counts currently in the Console.</summary>
        public static string ConsoleCounts()
        {
            var t = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
            object[] args = { 0, 0, 0 };
            t.GetMethod("GetCountsByType").Invoke(null, args);
            return $"errors={args[0]} warnings={args[1]} logs={args[2]}";
        }
    }
}
