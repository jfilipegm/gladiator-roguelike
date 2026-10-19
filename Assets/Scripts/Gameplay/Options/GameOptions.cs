using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

namespace Gladiator.Gameplay
{
    /// <summary>The player's settings (CLAUDE.md §4: volumes, look sensitivity, FOV, screen shake, render scale).</summary>
    [Serializable]
    public sealed class OptionsData
    {
        public int version = 1;
        [Range(0f, 1f)] public float master = 0.8f;
        [Range(0f, 1f)] public float sfx = 0.8f;
        [Range(0f, 1f)] public float crowd = 0.7f;
        [Range(0f, 1f)] public float music = 0.7f;
        [Tooltip("Multiplies mouse and stick look speed.")]
        public float lookSensitivity = 1f;
        public bool invertY;
        [Tooltip("Camera field of view at rest (degrees); the sprint FOV kick adds to it.")]
        public float fov = 62f;
        [Range(0f, 1f)] public float screenShake = 1f;
        [Range(0.5f, 1f)] public float renderScale = 1f;

        public void Clamp()
        {
            master = Mathf.Clamp01(master); sfx = Mathf.Clamp01(sfx); crowd = Mathf.Clamp01(crowd); music = Mathf.Clamp01(music);
            lookSensitivity = Mathf.Clamp(lookSensitivity, 0.2f, 3f);
            fov = Mathf.Clamp(fov, 50f, 90f);
            screenShake = Mathf.Clamp01(screenShake);
            renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
        }
    }

    /// <summary>
    /// Loads, applies and saves OptionsData (`options.json` in persistentDataPath, apart from the progress save so
    /// wiping progress keeps your settings). Volumes go to the mixer's exposed parameters (MasterVolume, SfxVolume,
    /// CrowdVolume, MusicVolume), shake to ScreenShake.UserScale, render scale to the URP asset; the camera rig reads
    /// sensitivity, invert and FOV every frame.
    /// </summary>
    public static class GameOptions
    {
        public const string FileName = "options.json";
        public static readonly string[] MixerParams = { "MasterVolume", "SfxVolume", "CrowdVolume", "MusicVolume" };

        /// <summary>Tests point this at a temp file.</summary>
        public static string PathOverride;
        /// <summary>The options screen is up (the pause toggle and menus leave Esc to it).</summary>
        public static bool PanelOpen
        {
            get => _panelOpen;
            set { if (_panelOpen && !value) _closedFrame = Time.frameCount; _panelOpen = value; }
        }
        static bool _panelOpen;
        static int _closedFrame = -1;
        /// <summary>Open, or closed this very frame (the Esc that closed it must not also pause / quit).</summary>
        public static bool PanelOwnsInput => _panelOpen || Time.frameCount == _closedFrame;

        static OptionsData _data;
        static AudioMixer _mixer;

        public static OptionsData Data => _data ??= Load();
        static string FilePath => !string.IsNullOrEmpty(PathOverride) ? PathOverride : Path.Combine(Application.persistentDataPath, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _data = null; _mixer = null; _panelOpen = false; _closedFrame = -1; }

        public static void Reload() => _data = null;

        static OptionsData Load()
        {
            OptionsData d = null;
            try
            {
                if (File.Exists(FilePath)) d = JsonUtility.FromJson<OptionsData>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Debug.LogWarning($"[Options] unreadable ({e.Message}); using defaults"); }
            d ??= new OptionsData();
            d.Clamp();
            return d;
        }

        public static void Save()
        {
            try
            {
                Data.Clamp();
                string path = FilePath, tmp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e) { Debug.LogError($"[Options] couldn't save: {e.Message}"); }
        }

        /// <summary>Pushes the settings to the mixer (remembered from the first call), screen shake and render scale.</summary>
        public static void Apply(AudioMixer mixer = null)
        {
            if (mixer != null) _mixer = mixer;
            var d = Data;
            if (_mixer != null)
            {
                _mixer.SetFloat(MixerParams[0], Db(d.master));
                _mixer.SetFloat(MixerParams[1], Db(d.sfx));
                _mixer.SetFloat(MixerParams[2], Db(d.crowd));
                _mixer.SetFloat(MixerParams[3], Db(d.music));
            }
            ScreenShake.UserScale = d.screenShake;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp &&
                !Mathf.Approximately(urp.renderScale, d.renderScale))
                urp.renderScale = d.renderScale; // the Editor restores the asset's value after Play mode (OptionsEditorGuard)
        }

        /// <summary>0..1 slider → decibels (−80 dB = silent).</summary>
        public static float Db(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
    }
}
