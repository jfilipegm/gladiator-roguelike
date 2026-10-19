using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Builds the AudioMixer (CLAUDE.md §4): Master → SFX / Crowd / Music, each group's volume exposed as MasterVolume /
    /// SfxVolume / CrowdVolume / MusicVolume (GameOptions sets them). Unity has no public API for creating mixers, so
    /// this goes through the Editor's internal AudioMixerController by reflection; if a future Unity changes it, this
    /// logs an error and returns null, and SfxPlayer still plays (without per-bus volumes).
    /// </summary>
    public static class MixerSetup
    {
        static readonly (string group, string param)[] Buses =
        {
            ("SFX", "SfxVolume"), ("Crowd", "CrowdVolume"), ("Music", "MusicVolume"),
        };
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        public static AudioMixer Ensure(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (existing != null) return existing;
            try
            {
                var asm = typeof(Editor).Assembly;
                var ctrlType = asm.GetType("UnityEditor.Audio.AudioMixerController");
                var groupType = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                var pathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
                var ctrl = ctrlType.GetMethod("CreateMixerControllerAtPath", All).Invoke(null, new object[] { path });
                var master = ctrlType.GetProperty("masterGroup", All).GetValue(ctrl);
                var create = ctrlType.GetMethod("CreateNewGroup", All);
                var addChild = ctrlType.GetMethod("AddChildToParent", All);
                var expose = ctrlType.GetMethod("AddExposedParameter", All);
                var volumeGuid = groupType.GetMethod("GetGUIDForVolume", All);
                var exposedProp = ctrlType.GetProperty("exposedParameters", All);

                void Expose(object group, string name)
                {
                    var guid = (GUID)volumeGuid.Invoke(group, null);
                    var p = Activator.CreateInstance(pathType, All, null, new[] { group, (object)guid }, null);
                    expose.Invoke(ctrl, new[] { p });
                    var list = (Array)exposedProp.GetValue(ctrl);
                    for (int i = 0; i < list.Length; i++)
                    {
                        var e = list.GetValue(i);
                        var gField = e.GetType().GetField("guid", All);
                        if ((GUID)gField.GetValue(e) != guid) continue;
                        e.GetType().GetField("name", All).SetValue(e, name);
                        list.SetValue(e, i);
                    }
                    exposedProp.SetValue(ctrl, list);
                }

                Expose(master, "MasterVolume");
                foreach (var (groupName, param) in Buses)
                {
                    var g = create.Invoke(ctrl, new object[] { groupName, false });
                    addChild.Invoke(ctrl, new[] { g, master });
                    Expose(g, param);
                }
                EditorUtility.SetDirty((UnityEngine.Object)ctrl);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            }
            catch (Exception e)
            {
                Debug.LogError("[MixerSetup] couldn't build the mixer through the Editor internals: " + e);
                return null;
            }
        }

        public static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            var found = mixer.FindMatchingGroups(name);
            foreach (var g in found) if (g.name == name) return g;
            return found.Length > 0 ? found[0] : null;
        }
    }
}
