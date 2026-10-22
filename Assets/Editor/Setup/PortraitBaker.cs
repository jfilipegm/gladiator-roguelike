using System;
using System.Collections.Generic;
using System.IO;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// The pictures of the select screens and the Codex roster (R9), all rendered from our own models and scenes:
    /// - every GladiatorDef's model, head and shoulders from the front-left (the Codex atlas's light and palette look),
    ///   128 px → Assets/Art/UI/Portraits/PT_&lt;id&gt;.png and PT_&lt;id&gt;_sil.png (the same in ink: locked);
    /// - every BossDef's model → PT_boss_&lt;id&gt;.png;
    /// - every RunDef's first arena scene from a fixed high three-quarter camera, 192×108 (rendered at 2× and box
    ///   filtered) → Assets/Art/UI/Arenas/AT_&lt;run id&gt;.png.
    /// All import as point-filtered sprites and are assigned to GladiatorDef.portrait / portraitSilhouette and
    /// RunDef.thumbnail / bossPortrait; RunTaglines fills the runs' words. Any number of gladiators, bosses and runs:
    /// a new one gets its pictures on the next bake. Opening the arena scenes needs every open scene saved (else the
    /// thumbnails are skipped with an error); the open scenes are put back afterwards.
    /// </summary>
    public static class PortraitBaker
    {
        public const string PortraitDir = "Assets/Art/UI/Portraits/";
        public const string ArenaDir = "Assets/Art/UI/Arenas/";
        public const int Size = 128;
        public const int ThumbW = 192, ThumbH = 108;
        const int ThumbSuper = 2;
        const float ArenaFov = 34f;
        /// <summary>Faces: a little above, 30° off the front (the model faces +Z), as the Codex atlas.</summary>
        static readonly Quaternion PortraitView = Quaternion.Euler(8f, 150f, 0f);
        static readonly Quaternion BodyView = Quaternion.Euler(12f, 150f, 0f);
        /// <summary>Arenas: from the south-west, high (the player spawns in the south facing north).</summary>
        static readonly Quaternion ArenaView = Quaternion.Euler(38f, 25f, 0f);
        static readonly Color32 Ink = new Color32(0x1E, 0x1B, 0x19, 0xFF);
        /// <summary>Model parts the bakes hide (R3: Asterion's trailing chain would shrink his full-body Codex picture to
        /// ~60 %, and it isn't his face). A model with none of these bakes as before.</summary>
        public static readonly string[] PortraitHidden = { "Chain" };

        [MenuItem("Gladiator/Setup/UI/Bake Portraits")]
        public static string BakeAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[PortraitBaker] leave Play mode first");
                return "playing";
            }
            EnsureFolder(PortraitDir);
            EnsureFolder(ArenaDir);
            int g = BakeGladiators(), b = BakeBosses();
            var runs = AllRuns();
            foreach (var r in runs) RunTaglines.Apply(r);
            AssetDatabase.SaveAssets(); // the arena pass opens scenes (Single), which unloads assets nothing references
            int a = BakeArenas(runs);
            runs = AllRuns(); // the scene switches unloaded the old RunDef objects: load them again
            foreach (var r in runs) Link(r);
            AssetDatabase.SaveAssets();
            string msg = $"[PortraitBaker] {g} gladiator portraits, {b} boss portraits, {a} of {runs.Length} arena thumbnails";
            Debug.Log(msg);
            return msg;
        }

        // ------------------------------------------------------------------ the rig (shared with CodexSetup's atlas)

        /// <summary>
        /// A preview scene with an orthographic camera and the key light: renders one model at a time into its target.
        /// The palette shader's hemisphere ambient is a global (EnvironmentLighting sets it per scene): the rig pins
        /// EnvironmentLighting's defaults while it lives, so a bake doesn't depend on which scene is open.
        /// </summary>
        public sealed class Rig : IDisposable
        {
            static readonly int SkyId = Shader.PropertyToID("_GLAmbientSky");
            static readonly int EquatorId = Shader.PropertyToID("_GLAmbientEquator");
            static readonly int GroundId = Shader.PropertyToID("_GLAmbientGround");

            readonly Scene _scene;
            readonly Camera _cam;
            readonly RenderTexture _rt;
            readonly Color _sky, _equator, _ground;

            public Rig(int width, int height)
            {
                _sky = Shader.GetGlobalColor(SkyId);
                _equator = Shader.GetGlobalColor(EquatorId);
                _ground = Shader.GetGlobalColor(GroundId);
                Shader.SetGlobalColor(SkyId, DefaultSky);
                Shader.SetGlobalColor(EquatorId, DefaultEquator);
                Shader.SetGlobalColor(GroundId, DefaultGround);

                _scene = EditorSceneManager.NewPreviewScene();
                _rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var camGo = new GameObject("PortraitCam");
                SceneManager.MoveGameObjectToScene(camGo, _scene);
                _cam = camGo.AddComponent<Camera>();
                _cam.scene = _scene;
                _cam.cameraType = CameraType.Preview;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _cam.orthographic = true;
                _cam.nearClipPlane = 0.05f;
                _cam.farClipPlane = 50f;
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.targetTexture = _rt;
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                var key = new GameObject("Key").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(key.gameObject, _scene);
                key.type = LightType.Directional;
                key.intensity = 1.3f;
                key.color = new Color(1f, 0.96f, 0.9f);
                key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            }

            /// <summary>Renders `model` (its renderers set to `material` unless null): head and shoulders from the
            /// front-left, or the whole body (the Codex atlas). A model with no "Head" part is cropped to its top. Parts
            /// named in PortraitHidden are left out of both.</summary>
            public void Render(GameObject model, Material material, bool headAndShoulders)
            {
                var inst = Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(inst, _scene);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                try
                {
                    var rends = inst.GetComponentsInChildren<Renderer>(true);
                    if (HideParts(inst.transform)) rends = Array.FindAll(rends, r => r.gameObject.activeInHierarchy);
                    if (material != null) foreach (var r in rends) r.sharedMaterial = material;
                    var all = Bounds(rends, inst.transform.position + Vector3.up);
                    Vector3 centre;
                    float size;
                    Quaternion rot;
                    var head = headAndShoulders ? FindPart(inst.transform, "Head") : null;
                    if (head != null)
                    {
                        var hb = Bounds(head.GetComponentsInChildren<Renderer>(true), head.position);
                        float hh = Mathf.Max(hb.size.y, 0.05f);
                        float top = hb.max.y + hh * 0.1f, bottom = hb.min.y - hh * 0.6f;
                        centre = new Vector3(hb.center.x, (top + bottom) * 0.5f, hb.center.z);
                        size = Mathf.Max((top - bottom) * 0.5f, Mathf.Max(hb.extents.x, hb.extents.z) * 1.25f);
                        rot = PortraitView;
                    }
                    else if (headAndShoulders)
                    {
                        float top = all.max.y, bottom = all.max.y - all.size.y * 0.55f;
                        centre = new Vector3(all.center.x, (top + bottom) * 0.5f, all.center.z);
                        size = Mathf.Max((top - bottom) * 0.5f, Mathf.Max(all.extents.x, all.extents.z)) * 1.05f;
                        rot = PortraitView;
                    }
                    else
                    {
                        centre = all.center;
                        size = Mathf.Max(all.extents.y, Mathf.Max(all.extents.x, all.extents.z)) * 1.12f;
                        rot = BodyView;
                    }
                    float back = all.extents.magnitude + 5f;
                    _cam.farClipPlane = back * 2f + 10f;
                    _cam.transform.rotation = rot;
                    _cam.transform.position = centre - rot * Vector3.forward * back;
                    _cam.orthographicSize = size;
                    _cam.Render();
                }
                finally
                {
                    Object.DestroyImmediate(inst);
                }
            }

            /// <summary>Copies the last render into `dst` at (x, y) (call dst.Apply after).</summary>
            public void CopyTo(Texture2D dst, int x, int y)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                dst.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), x, y);
                RenderTexture.active = prev;
            }

            public void Dispose()
            {
                RenderTexture.ReleaseTemporary(_rt);
                EditorSceneManager.ClosePreviewScene(_scene);
                Shader.SetGlobalColor(SkyId, _sky);
                Shader.SetGlobalColor(EquatorId, _equator);
                Shader.SetGlobalColor(GroundId, _ground);
            }
        }

        // EnvironmentLighting's default hemisphere (the look a fresh scene gets)
        static readonly Color DefaultSky = new Color(0.60f, 0.72f, 0.90f);
        static readonly Color DefaultEquator = new Color(0.60f, 0.60f, 0.58f);
        static readonly Color DefaultGround = new Color(0.42f, 0.36f, 0.30f);

        static Bounds Bounds(Renderer[] rends, Vector3 fallback)
        {
            var b = new Bounds(fallback, Vector3.zero);
            bool first = true;
            foreach (var r in rends)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Sets every part named in PortraitHidden inactive under `root`; true if any was.</summary>
        static bool HideParts(Transform root)
        {
            bool any = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && Array.IndexOf(PortraitHidden, t.name) >= 0) { t.gameObject.SetActive(false); any = true; }
            return any;
        }

        /// <summary>The part named `name` (else the first whose name starts with it), anywhere under `root`.</summary>
        static Transform FindPart(Transform root, string name)
        {
            Transform prefix = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (t.name == name) return t;
                if (prefix == null && t.name.StartsWith(name, StringComparison.Ordinal)) prefix = t;
            }
            return prefix;
        }

        // ------------------------------------------------------------------ portraits

        static int BakeGladiators()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            int n = 0;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                using (var rig = new Rig(Size, Size))
                    foreach (var g in All<GladiatorDef>())
                    {
                        // no model of its own (the Nobody) = the player prefab's, CH_Nobody
                        var model = g.model != null ? g.model : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Characters/CH_Nobody.fbx");
                        if (model == null) continue;
                        rig.Render(model, mat, true);
                        rig.CopyTo(tex, 0, 0);
                        tex.Apply();
                        string path = PortraitDir + "PT_" + g.id + ".png", sil = PortraitDir + "PT_" + g.id + "_sil.png";
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                        var ink = Silhouette(tex);
                        File.WriteAllBytes(sil, ink.EncodeToPNG());
                        Object.DestroyImmediate(ink);
                        g.portrait = ImportSprite(path);
                        g.portraitSilhouette = ImportSprite(sil);
                        EditorUtility.SetDirty(g);
                        n++;
                    }
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            return n;
        }

        static int BakeBosses()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            int n = 0;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                using (var rig = new Rig(Size, Size))
                    foreach (var b in All<BossDef>())
                    {
                        if (b.enemy == null || b.enemy.model == null) continue;
                        rig.Render(b.enemy.model, mat, true);
                        rig.CopyTo(tex, 0, 0);
                        tex.Apply();
                        string path = BossPortraitPath(b);
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                        ImportSprite(path);
                        n++;
                    }
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            return n;
        }

        public static string BossPortraitPath(BossDef b) => PortraitDir + "PT_boss_" + b.id + ".png";
        public static string ThumbnailPath(RunDef r) => ArenaDir + "AT_" + r.id + ".png";

        /// <summary>The same picture in ink, alpha kept.</summary>
        static Texture2D Silhouette(Texture2D src)
        {
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0) px[i] = new Color32(Ink.r, Ink.g, Ink.b, px[i].a);
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Points the run at pictures already baked (the menu build calls it too: no rendering).</summary>
        public static void Link(RunDef r)
        {
            if (r == null) return;
            var thumb = AssetDatabase.LoadAssetAtPath<Sprite>(ThumbnailPath(r));
            var boss = r.Boss;
            var face = boss != null ? AssetDatabase.LoadAssetAtPath<Sprite>(BossPortraitPath(boss)) : null;
            if (thumb != null && r.thumbnail != thumb) { r.thumbnail = thumb; EditorUtility.SetDirty(r); }
            if (face != null && r.bossPortrait != face) { r.bossPortrait = face; EditorUtility.SetDirty(r); }
        }

        // ------------------------------------------------------------------ arena thumbnails

        static int BakeArenas(RunDef[] runs)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[PortraitBaker] save the open scene(s) first: the arena thumbnails open each arena scene. Skipped them.");
                    return 0;
                }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int w = ThumbW * ThumbSuper, h = ThumbH * ThumbSuper, n = 0;
            // DontSave: opening each arena scene (Single) unloads unused assets, which would destroy a plain texture
            var big = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                var assetPaths = new string[runs.Length];
                for (int k = 0; k < runs.Length; k++) assetPaths[k] = AssetDatabase.GetAssetPath(runs[k]);
                for (int k = 0; k < runs.Length; k++)
                {
                    var r = AssetDatabase.LoadAssetAtPath<RunDef>(assetPaths[k]); // the previous scene switch unloaded it
                    if (r == null) continue;
                    string scenePath = r.arenas != null && r.arenas.Length > 0 ? r.arenas[0].scenePath : null;
                    if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
                    {
                        Debug.LogWarning("[PortraitBaker] " + r.id + ": no arena scene at '" + scenePath + "' yet (build it, then bake again)");
                        continue;
                    }
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    r = AssetDatabase.LoadAssetAtPath<RunDef>(assetPaths[k]); // and this one may have unloaded it again
                    var camGo = new GameObject("ThumbnailCam") { hideFlags = HideFlags.HideAndDontSave };
                    bool fog = RenderSettings.fog;
                    try
                    {
                        var cam = camGo.AddComponent<Camera>();
                        cam.fieldOfView = ArenaFov;
                        cam.nearClipPlane = 0.5f;
                        cam.farClipPlane = 2000f;
                        cam.clearFlags = CameraClearFlags.Skybox;
                        cam.allowMSAA = false;
                        cam.targetTexture = rt;
                        cam.aspect = w / (float)h;
                        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                        data.renderPostProcessing = true;
                        Aim(cam, r.layout);
                        RenderSettings.fog = false; // the camera stands far off: fog meant for the player's eye would wash it out
                        cam.Render();
                        RenderSettings.fog = fog;
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        big.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        RenderTexture.active = prev;
                        big.Apply();
                        var small = Downsample(big, ThumbSuper);
                        string path = ThumbnailPath(r);
                        File.WriteAllBytes(path, small.EncodeToPNG());
                        Object.DestroyImmediate(small);
                        r.thumbnail = ImportSprite(path);
                        EditorUtility.SetDirty(r);
                        AssetDatabase.SaveAssetIfDirty(r); // before the next scene switch unloads it
                        n++;
                    }
                    finally
                    {
                        RenderSettings.fog = fog;
                        Object.DestroyImmediate(camGo);
                    }
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(big);
                Restore(setup);
            }
            return n;
        }

        /// <summary>Frames the floor (the layout's play area, walls included) from ArenaView: the nearest distance at
        /// which all of it is in view.</summary>
        static void Aim(Camera cam, ArenaLayoutDef l)
        {
            Vector2 min, max;
            if (l != null && l.outline != null && l.outline.Length >= 3)
            {
                min = max = l.outline[0];
                foreach (var p in l.outline) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            }
            else
            {
                var c = l != null ? new Vector2(l.center.x, l.center.z) : Vector2.zero;
                var half = l != null ? new Vector2(l.radius, l.radius * (l.aspect > 0f ? l.aspect : 1f)) : new Vector2(40f, 40f);
                min = c - half;
                max = c + half;
            }
            var pad = (max - min) * 0.06f;
            min -= pad;
            max += pad;
            var centre = new Vector3((min.x + max.x) * 0.5f, 2f, (min.y + max.y) * 0.5f);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? 0f : 7f, (i & 4) == 0 ? min.y : max.y);
            cam.transform.rotation = ArenaView;
            var fwd = ArenaView * Vector3.forward;
            float lo = 5f, hi = 1500f;
            for (int it = 0; it < 30; it++)
            {
                float d = (lo + hi) * 0.5f;
                cam.transform.position = centre - fwd * d;
                bool fits = true;
                foreach (var p in corners)
                {
                    var v = cam.WorldToViewportPoint(p);
                    if (v.z < cam.nearClipPlane || v.x < 0.01f || v.x > 0.99f || v.y < 0.01f || v.y > 0.99f) { fits = false; break; }
                }
                if (fits) hi = d;
                else lo = d;
            }
            cam.transform.position = centre - fwd * hi;
        }

        /// <summary>Averages `k`×`k` blocks (clean edges at the small size).</summary>
        static Texture2D Downsample(Texture2D src, int k)
        {
            int w = src.width / k, h = src.height / k;
            var a = src.GetPixels32();
            var o = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, b = 0, al = 0;
                    for (int j = 0; j < k; j++)
                        for (int i = 0; i < k; i++)
                        {
                            var c = a[(y * k + j) * src.width + x * k + i];
                            r += c.r; g += c.g; b += c.b; al += c.a;
                        }
                    int q = k * k;
                    o[y * w + x] = new Color32((byte)(r / q), (byte)(g / q), (byte)(b / q), (byte)(al / q));
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(o);
            t.Apply();
            return t;
        }

        /// <summary>Puts the open scenes back (an untitled scene can't be reopened: a new empty one instead).</summary>
        static void Restore(SceneSetup[] setup)
        {
            var kept = new List<SceneSetup>();
            foreach (var s in setup) if (!string.IsNullOrEmpty(s.path) && File.Exists(s.path)) kept.Add(s);
            if (kept.Count == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }
            bool active = false;
            foreach (var s in kept) active |= s.isActive;
            if (!active) kept[0].isActive = true;
            EditorSceneManager.RestoreSceneManagerSetup(kept.ToArray());
        }

        // ------------------------------------------------------------------ assets

        static T[] All<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list.ToArray();
        }

        /// <summary>Every RunDef (any folder), the ladder's ones first in the ladder's order.</summary>
        static RunDef[] AllRuns()
        {
            var list = new List<RunDef>();
            foreach (var r in ProvincialPitSetup.AllRuns()) if (r != null && !list.Contains(r)) list.Add(r);
            foreach (var r in All<RunDef>()) if (!list.Contains(r)) list.Add(r);
            return list.ToArray();
        }

        static void EnsureFolder(string dir)
        {
            dir = dir.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }

        /// <summary>Imports a baked PNG as a point-filtered, uncompressed sprite with no mips.</summary>
        static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
