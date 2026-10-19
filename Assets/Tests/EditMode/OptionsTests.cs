using System.IO;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 4: options save / load / clamp, volume → decibels.</summary>
    public class OptionsTests
    {
        string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "options_test.json");
            if (File.Exists(_path)) File.Delete(_path);
            GameOptions.PathOverride = _path;
            GameOptions.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            GameOptions.PathOverride = null;
            GameOptions.Reload();
        }

        [Test]
        public void Defaults_WhenThereIsNoFile()
        {
            var d = GameOptions.Data;
            Assert.AreEqual(62f, d.fov);
            Assert.AreEqual(1f, d.screenShake);
            Assert.AreEqual(1f, d.lookSensitivity);
            Assert.IsFalse(d.invertY);
        }

        [Test]
        public void Save_ThenLoad_KeepsTheSettings()
        {
            var d = GameOptions.Data;
            d.master = 0.3f; d.fov = 75f; d.invertY = true; d.screenShake = 0.25f; d.renderScale = 0.75f;
            GameOptions.Save();
            GameOptions.Reload();
            var back = GameOptions.Data;
            Assert.AreNotSame(d, back);
            Assert.AreEqual(0.3f, back.master, 1e-4f);
            Assert.AreEqual(75f, back.fov);
            Assert.IsTrue(back.invertY);
            Assert.AreEqual(0.25f, back.screenShake, 1e-4f);
            Assert.AreEqual(0.75f, back.renderScale, 1e-4f);
        }

        [Test]
        public void HandEditedFile_IsClamped()
        {
            File.WriteAllText(_path, "{\"fov\": 200, \"master\": 4, \"renderScale\": 0.1, \"lookSensitivity\": -3}");
            GameOptions.Reload();
            var d = GameOptions.Data;
            Assert.AreEqual(90f, d.fov);
            Assert.AreEqual(1f, d.master);
            Assert.AreEqual(0.5f, d.renderScale);
            Assert.AreEqual(0.2f, d.lookSensitivity, 1e-4f);
        }

        [Test]
        public void Volume_ToDecibels()
        {
            Assert.AreEqual(0f, GameOptions.Db(1f), 1e-4f);
            Assert.AreEqual(-6.02f, GameOptions.Db(0.5f), 0.01f);
            Assert.AreEqual(-80f, GameOptions.Db(0f), "zero is silent");
        }
    }
}
