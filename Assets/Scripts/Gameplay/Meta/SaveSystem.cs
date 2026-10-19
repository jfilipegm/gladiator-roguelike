using System;
using System.IO;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Reads and writes SaveData as JSON in persistentDataPath. Writes go to a temp file first and then replace the
    /// save, so a crash mid-write never leaves half a file. A file that won't parse is kept as `.corrupt` and the game
    /// starts fresh rather than refusing to boot. Bot/stress runs use their own file so they never touch the player's, and
    /// so does the Editor: on Linux the Editor and the built game share persistentDataPath, and Editor play sessions
    /// (development checks) had been writing discoveries and unlocks into the player's save.
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "save.json";
        public const string BotFileName = "save_bot.json";
        public const string EditorFileName = "save_editor.json";

        /// <summary>Tests point this at a temp file (never reset automatically: the test fixture owns it).</summary>
        public static string PathOverride;

        public static string FilePath
        {
            get
            {
                if (!string.IsNullOrEmpty(PathOverride)) return PathOverride;
                bool bot = CommandLine.Has("-bot") || CommandLine.Has("-stress");
                return Path.Combine(Application.persistentDataPath, bot ? BotFileName : Application.isEditor ? EditorFileName : FileName);
            }
        }

        public static SaveData Load() => Load(FilePath);

        public static SaveData Load(string path)
        {
            if (!File.Exists(path)) return new SaveData();
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null) throw new FormatException("empty save");
                data.Migrate();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] {path} is unreadable ({e.Message}); keeping it as .corrupt and starting fresh");
                try { File.Copy(path, path + ".corrupt", true); } catch (Exception) { /* best effort */ }
                return new SaveData();
            }
        }

        public static bool Save(SaveData data) => Save(data, FilePath);

        public static bool Save(SaveData data, string path)
        {
            if (data == null) return false;
            try
            {
                data.version = SaveData.CurrentVersion;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] couldn't write {path}: {e.Message}");
                return false;
            }
        }
    }
}
