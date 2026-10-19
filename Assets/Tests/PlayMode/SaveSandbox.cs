using System.IO;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Every PlayMode test runs against a throwaway save file, so runs that end inside tests (deaths, Gorm kills) never
/// touch the player's real progress (or options). Global namespace = applies to every fixture in this assembly.
/// </summary>
[SetUpFixture]
public class SaveSandbox
{
    public static string TestPath => Path.Combine(Application.temporaryCachePath, "playmode_test_save.json");
    public static string OptionsPath => Path.Combine(Application.temporaryCachePath, "playmode_test_options.json");

    [OneTimeSetUp]
    public void Redirect()
    {
        SaveSystem.PathOverride = TestPath;
        GameOptions.PathOverride = OptionsPath;
        GameOptions.Reload();
        Wipe();
    }

    [OneTimeTearDown]
    public void Restore()
    {
        Wipe();
        if (File.Exists(OptionsPath)) File.Delete(OptionsPath);
        SaveSystem.PathOverride = null;
        GameOptions.PathOverride = null;
        GameOptions.Reload();
        MetaProgress.Reload();
    }

    /// <summary>Fresh, empty progress (only ever the sandbox file).</summary>
    public static void Wipe()
    {
        if (SaveSystem.PathOverride != TestPath) throw new System.InvalidOperationException("save sandbox not active");
        foreach (var f in new[] { TestPath, TestPath + ".tmp", TestPath + ".corrupt" }) if (File.Exists(f)) File.Delete(f);
        MetaProgress.Reload();
    }
}
