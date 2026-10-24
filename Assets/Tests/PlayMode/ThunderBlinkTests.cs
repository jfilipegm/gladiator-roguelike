using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Gladiator.Tests
{
    /// <summary>
    /// Note 35 (the producer, v0.28.0: the Thunder Champion's blink "is sudden, I at first thought it was a bug"; an icon
    /// should say when it's ready). The HUD's blink tile shows only for him, under the status plate's right corner with
    /// SLIDE under it; it shows the dash ready, fills while it recharges, pops when it's back. The blink still goes as far
    /// (the gameplay tests stay in ThunderChampionTests); the camera's look target starts where he was and eases onto
    /// him instead of cutting, and the camera ends at the new spot; after-images line the path and fade; none of the
    /// per-frame drawing allocates.
    /// </summary>
    public class ThunderBlinkTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        static GladiatorDef Thunder => UnityEditor.AssetDatabase.LoadAssetAtPath<GladiatorDef>("Assets/Data/Gladiators/GL_ThunderChampion.asset");
        static GladiatorDef Nobody => UnityEditor.AssetDatabase.LoadAssetAtPath<GladiatorDef>("Assets/Data/Gladiators/GL_Nobody.asset");

        Hud _hud;
        LightningDash _dash;

        IEnumerator Boot(GladiatorDef as_)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = as_;
            var op = SceneManager.LoadSceneAsync(TestArena, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll();
            _hud = Object.FindAnyObjectByType<Hud>();
            _dash = Player.Main.GetComponent<LightningDash>();
            Assert.IsNotNull(_hud, "the arena has the HUD");
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static Rect World(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator BlinkIcon_OnlyForTheThunderChampion_UnderThePlatesCorner_WithSlide()
        {
            yield return Boot(Nobody);
            Assert.IsTrue(_dash == null, "the Nobody has no blink"); // Unity's null (GetComponent's fake null in the Editor)
            Assert.IsFalse(_hud.BlinkIcon.Shown, "so no blink tile");
            Assert.IsFalse(_hud.BlinkIcon.Root.gameObject.activeSelf);

            yield return Boot(Thunder);
            Assert.IsNotNull(_dash);
            Assert.IsTrue(_hud.BlinkIcon.Shown, "the Thunder Champion's blink tile");
            Assert.IsTrue(_hud.BlinkIcon.Root.gameObject.activeInHierarchy);
            Assert.AreEqual(BlinkReadyIcon.KeyWord, _hud.BlinkIcon.KeyText, "it says what blinks: the slide");
            Canvas.ForceUpdateCanvases();
            float tol = _hud.CanvasRect.lossyScale.x + 0.5f;
            var plate = World(_hud.StatusPlateRect);
            var tile = World(_hud.BlinkIcon.Tile);
            var root = World(_hud.BlinkIcon.Root);
            Assert.LessOrEqual(tile.yMax, plate.yMin + tol, "under the status plate");
            Assert.AreEqual(plate.xMax, root.xMax, tol, "its right edge on the plate's");
            Assert.GreaterOrEqual(tile.xMin, root.xMin - tol, "the tile inside its widget");
            Assert.LessOrEqual(tile.xMax, root.xMax + tol);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator BlinkIcon_ReadyThenFillingThenPopsBack()
        {
            yield return Boot(Thunder);
            var icon = _hud.BlinkIcon;
            yield return null;
            Assert.IsTrue(icon.ShowsReady, "ready at the start");
            Assert.AreEqual(1f, icon.LitFill, 1e-4f, "the bolt lit");
            Assert.AreEqual(0, icon.Pops, "no pop for the dash he spawns with");

            Assert.IsTrue(_dash.Perform(Vector3.right));
            yield return null;
            Assert.IsFalse(icon.ShowsReady, "recharging");
            Assert.Less(icon.LitFill, 0.25f, "the bolt nearly empty just after the blink");
            Assert.AreEqual(_dash.Charge01, icon.LitFill, 1e-4f, "the fill is the dash's charge");

            float cd = _dash.trait.cooldown;
            yield return new WaitForSeconds(cd * 0.5f);
            Assert.IsFalse(icon.ShowsReady);
            Assert.AreEqual(0.5f, icon.LitFill, 0.15f, "half charged half way");

            float end = Time.time + cd;
            while (!_dash.Ready && Time.time < end) yield return null;
            Assert.IsTrue(_dash.Ready);
            // the frame it came back has run whole: the dash ticks (Player) before the HUD draws (Late), and the
            // GameLoop before this coroutine
            Assert.IsTrue(icon.ShowsReady, "lit again");
            Assert.AreEqual(1f, icon.LitFill, 1e-4f);
            Assert.AreEqual(1, icon.Pops, "one pop the moment it came back");
            Assert.IsTrue(icon.Popping, "and it's playing");
            Assert.Greater(icon.Tile.localScale.x, 1.05f, "the tile swells");
            yield return new WaitForSecondsRealtime(BlinkReadyIcon.PopSeconds + 0.1f);
            Assert.IsFalse(icon.Popping);
            Assert.Less(icon.Tile.localScale.x, 1.05f, "and settles (a gentle pulse only)");
            Assert.AreEqual(1, icon.Pops);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Blink_SameDistance_TheCameraGlidesOntoTheNewSpot()
        {
            yield return Boot(Thunder);
            var rig = PlayerCameraRig.Main;
            Assert.IsNotNull(rig, "the arena's camera rig");
            var target = Player.Main.transform.Find("CameraTarget");
            Vector3 rest = target.localPosition;
            var cam = Camera.main.transform;
            Vector3 camBefore = cam.position;
            float glide = _dash.trait.cameraGlide;
            Assert.Greater(glide, 0f);

            Assert.IsTrue(_dash.Perform(Vector3.right));
            Vector3 moved = _dash.LastTo - _dash.LastFrom;
            moved.y = 0f;
            Assert.AreEqual(_dash.trait.distance, moved.magnitude, 0.3f, "the blink goes as far as before");
            Assert.IsTrue(rig.Gliding, "the camera glides");
            Vector3 startAt = _dash.LastFrom + Player.Main.transform.TransformVector(rest);
            Assert.Less(Vector3.Distance(target.position, startAt), 0.05f, "its target starts where he was, not on him");

            // ease-out: after t seconds the target is (1 - t / glide)^3 of the blink away, and only ever closer
            float full = (_dash.LastFrom - _dash.LastTo).magnitude;
            Assert.AreEqual(full, rig.GlideOffset.magnitude, 1e-3f);
            float prev = full, t = 0f;
            int frames = 0;
            while (rig.Gliding && frames < 600)
            {
                yield return null; // the rig has ticked once more, on this frame's real delta (GameLoop runs before coroutines)
                t += Time.unscaledDeltaTime;
                frames++;
                float now = rig.GlideOffset.magnitude;
                Assert.LessOrEqual(now, prev + 1e-4f, "never further away");
                if (rig.Gliding)
                {
                    float k = 1f - t / glide;
                    Assert.AreEqual(full * k * k * k, now, 0.05f, "ease-out cubic, frame " + frames);
                }
                prev = now;
            }
            Assert.IsFalse(rig.Gliding, "the glide ends");
            Assert.GreaterOrEqual(frames, 2, "over several frames, not one cut");
            Assert.AreEqual(Vector3.zero, rig.GlideOffset);
            Assert.Less(Vector3.Distance(target.localPosition, rest), 1e-4f, "the target back on him");

            yield return new WaitForSeconds(0.5f); // the vcam's own damping settles
            Vector3 camMoved = cam.position - camBefore;
            camMoved.y = 0f;
            Assert.AreEqual(moved.magnitude, camMoved.magnitude, 1f, "the camera reached the new spot");
            Assert.Greater(Vector3.Dot(camMoved.normalized, moved.normalized), 0.9f, "the way he went");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Blink_LeavesAfterImagesAlongThePath_TheyFade_AndDrawingDoesntAllocate()
        {
            yield return Boot(Thunder);
            var t = _dash.trait;
            Assert.IsNotNull(t.ghostMaterial, "M_FxBlinkGhost linked: run Gladiator/Setup/Gladiators/Blink FX (note 35)");
            Assert.IsTrue(t.ghostMaterial.enableInstancing, "drawn instanced");
            Assert.GreaterOrEqual(_dash.AfterImageParts, 4, "drawn from his model's parts (body, head, arms, legs)");

            Assert.IsTrue(_dash.Perform(Vector3.right));
            Vector3 moved = _dash.LastTo - _dash.LastFrom;
            moved.y = 0f;
            int along = Mathf.Clamp(Mathf.RoundToInt(moved.magnitude / t.ghostSpacing), 1, BlinkGhosts.MaxGhosts - 1);
            Assert.AreEqual(along + (t.landingFlash > 0f ? 1 : 0), _dash.AfterImages, "after-images along the path + the landing shell");

            // the per-frame work (the after-images' draw, the HUD tile) allocates nothing
            var icon = _hud.BlinkIcon;
            var dash = _dash;
            TestDelegate frame = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    dash.Tick(0f, 0f); // dt 0: the paused path, the after-images drawn as they are
                    icon.Draw(dash, 0f);
                }
            };
            frame(); // warm up (JIT)
            Assert.That(frame, Is.Not.AllocatingGCMemory());

            yield return new WaitForSeconds(t.ghostLife + t.ghostSweep + 0.15f);
            Assert.AreEqual(0, _dash.AfterImages, "faded");
        }
    }
}
