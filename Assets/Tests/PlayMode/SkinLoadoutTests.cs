using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R6 Skins, the loadout (docs/tracks/R7-contract.md "R6-B"): the player spawns in the skin's model (a copy of the
    /// Nobody's, renamed, with a marker part and a stray material), the animator finds and drives every part of it, every
    /// renderer wears the one shared characterMaterial, a skin of another gladiator (or with no model) is ignored, no skin =
    /// the gladiator's own model, and GameSession.Skin dresses the player as the test override does.
    /// </summary>
    public class SkinLoadoutTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string NobodyModel = "Assets/Art/Models/Characters/CH_Nobody.fbx";
        const string SkinModelName = "CH_Nobody_TestSkin";
        const string Marker = "SkinTestMarker";
        static readonly string[] PartNames = { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" };

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Nobody => Load<GladiatorDef>("Assets/Data/Gladiators/GL_Nobody.asset");
        static GladiatorDef Knight => Load<GladiatorDef>("Assets/Data/Gladiators/GL_BloodKnight.asset");

        GameObject _holder, _skinModel;
        Material _stray;
        SkinDef _skin;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // The skin's "prefab": a copy of the Nobody's model under an inactive holder that survives the scene loads (so
            // it's never drawn itself, and its clone is active under the player), with a marker part and a material the
            // loadout must replace.
            _holder = new GameObject("SkinTestHolder");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            var fbx = Load<GameObject>(NobodyModel);
            Assert.IsNotNull(fbx, NobodyModel);
            _skinModel = Object.Instantiate(fbx, _holder.transform);
            _skinModel.name = SkinModelName;
            new GameObject(Marker).transform.SetParent(_skinModel.transform, false);
            _stray = new Material(Shader.Find("Sprites/Default")) { name = "StrayTestMaterial" };
            foreach (var r in _skinModel.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = _stray;
            _skin = Skin("nobody_test_skin", Nobody, _skinModel);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GladiatorLoadout.NextSkinOverride = null;
            GameSession.Clear();
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_holder != null) Object.Destroy(_holder);
            if (_stray != null) Object.Destroy(_stray);
        }

        SkinDef Skin(string id, GladiatorDef g, GameObject model)
        {
            var s = ScriptableObject.CreateInstance<SkinDef>();
            s.id = id; s.displayName = id; s.gladiator = g; s.model = model;
            _made.Add(s);
            return s;
        }

        static IEnumerator Boot(GladiatorDef as_, SkinDef skin)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = as_;
            GladiatorLoadout.NextSkinOverride = skin;
            var op = SceneManager.LoadSceneAsync(TestArena, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            yield return null;
        }

        static GladiatorLoadout Loadout => Player.Main.GetComponent<GladiatorLoadout>();

        /// <summary>The one model under the player's visual root.</summary>
        static Transform Model()
        {
            var visual = Loadout.visualRoot;
            Assert.IsNotNull(visual, "the loadout's visual root");
            Assert.AreEqual(1, visual.childCount, "one model: the old one is gone");
            return visual.GetChild(0);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Skin_ThePlayerSpawnsInItsModel()
        {
            yield return Boot(Nobody, _skin);
            Assert.AreSame(Nobody, Loadout.Current);
            Assert.AreSame(_skin, Loadout.CurrentSkin);
            Assert.IsNull(GladiatorLoadout.NextSkinOverride, "cleared after use");
            var model = Model();
            Assert.AreEqual(SkinModelName, model.name, "the skin's model, not the prefab's CH_Nobody");
            Assert.IsNotNull(FindDeep(model, Marker), "the skin's own part came along");
            Assert.IsTrue(model.gameObject.activeInHierarchy, "drawn");
            Assert.AreEqual(Player.Main.gameObject.layer, model.gameObject.layer, "on the player's layer");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Skin_TheAnimatorFindsAndDrivesEveryPart()
        {
            yield return Boot(Nobody, _skin);
            var model = Model();
            foreach (var name in PartNames) Assert.IsNotNull(FindDeep(model, name), name + ": the skin keeps the part names");

            // the animator's rig was built from the skin's model (the loadout runs first), not from the destroyed prefab one
            var animator = Player.Main.GetComponent<PlayerPartAnimator>();
            Assert.IsNotNull(animator);
            var rigField = typeof(PlayerPartAnimator).GetField("_rig", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rigField, "PlayerPartAnimator._rig (renamed? update this test)");
            var rig = (PartRig)rigField.GetValue(animator);
            Assert.IsNotNull(rig);
            Assert.IsTrue(rig.IsValid, "the rig found the Body");
            foreach (var field in new[] { "_body", "_head", "_armL", "_armR", "_legL", "_legR" })
            {
                var f = typeof(PartRig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(f, "PartRig." + field + " (renamed? update this test)");
                var part = (Transform)f.GetValue(rig);
                Assert.IsNotNull(part, field + " found");
                Assert.IsTrue(part.IsChildOf(model), field + " is the skin's part");
            }

            // running swings the skin's legs
            var legL = FindDeep(model, "LegL");
            var bind = legL.localRotation;
            var input = new ScriptedInput();
            Player.Main.OverrideSource = input;
            input.Next.Move = new Vector2(0f, 1f);
            float maxAngle = 0f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(bind, legL.localRotation));
                yield return null;
            }
            input.Next.Move = Vector2.zero;
            Assert.Greater(maxAngle, 5f, "the run cycle moves the skin's LegL");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Skin_EveryRendererWearsTheOneSharedMaterial()
        {
            yield return Boot(Nobody, _skin);
            var mat = Loadout.characterMaterial;
            Assert.IsNotNull(mat, "the loadout's characterMaterial");
            var model = Model();
            var rends = model.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(_skinModel.GetComponentsInChildren<Renderer>(true).Length, rends.Length, "every part's renderer");
            Assert.Greater(rends.Length, 0);
            var distinct = new HashSet<Material>();
            foreach (var r in Loadout.visualRoot.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    Assert.AreNotSame(_stray, m, r.name + " still wears the skin model's own material");
                    distinct.Add(m);
                }
            Assert.AreEqual(1, distinct.Count, "one distinct material on the player");
            Assert.IsTrue(distinct.Contains(mat), "and it's the shared characterMaterial");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Skin_OfAnotherGladiator_IsIgnored()
        {
            var knights = Skin("blood_knight_test_skin", Knight, _skinModel);
            yield return Boot(Nobody, knights);
            Assert.AreSame(Nobody, Loadout.Current);
            Assert.IsNull(Loadout.CurrentSkin, "the knight's skin isn't the Nobody's");
            Assert.AreEqual("CH_Nobody", Model().name, "the prefab's own model");
            Assert.IsNull(FindDeep(Model(), Marker));

            yield return Boot(Knight, _skin);
            Assert.AreSame(Knight, Loadout.Current);
            Assert.IsNull(Loadout.CurrentSkin, "the Nobody's skin isn't the knight's");
            Assert.AreEqual("CH_BloodKnight", Model().name, "his own model");

            var empty = Skin("nobody_no_model", Nobody, null);
            yield return Boot(Nobody, empty);
            Assert.IsNull(Loadout.CurrentSkin, "a skin with no model: the default look");
            Assert.AreEqual("CH_Nobody", Model().name);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NoSkin_TheGladiatorsOwnModel()
        {
            yield return Boot(Knight, null);
            Assert.AreSame(Knight, Loadout.Current);
            Assert.IsNull(Loadout.CurrentSkin);
            Assert.AreEqual("CH_BloodKnight", Model().name);
            foreach (var r in Model().GetComponentsInChildren<Renderer>(true))
                Assert.AreSame(Loadout.characterMaterial, r.sharedMaterial, r.name + ": the shared material, as before");

            yield return Boot(Nobody, null);
            Assert.IsNull(Loadout.CurrentSkin);
            Assert.AreEqual("CH_Nobody", Model().name, "the player prefab's own model");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GameSessionSkin_DressesThePlayer()
        {
            GameSession.Skin = _skin; // as the menu sets it at launch
            yield return Boot(Nobody, null);
            Assert.AreSame(_skin, Loadout.CurrentSkin, "GameSession.Skin");
            Assert.AreEqual(SkinModelName, Model().name);
            Assert.AreSame(_skin, GameSession.Skin, "kept for Retry");

            var other = Skin("nobody_other_skin", Nobody, Load<GameObject>(NobodyModel));
            GameSession.Skin = _skin;
            yield return Boot(Nobody, other);
            Assert.AreSame(other, Loadout.CurrentSkin, "the test override beats GameSession.Skin");
            Assert.AreEqual("CH_Nobody", Model().name, "the override's model");
        }
    }
}
