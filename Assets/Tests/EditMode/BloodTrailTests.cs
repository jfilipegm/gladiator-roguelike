using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// Note 32: the Blood Slide trail as blood smears. How a smear lies (on the ground the player slid on, along the
    /// slide, behind its mark), the shape hash (the same as PixelSrc/blood_smears.py's preview), and the assets
    /// Gladiator/Setup/Combat/Blood Trail makes: the atlas imported as data, the material, the renderer on the player.
    /// </summary>
    public class BloodTrailTests
    {
        const string TexturePath = "Assets/Art/Textures/FX/BloodSmears.png";
        const string MaterialPath = "Assets/Art/Materials/M_FxBloodTrail.mat";
        const string PlayerPrefab = "Assets/Prefabs/Player/P_Player.prefab";

        static void Near(Vector3 expected, Vector3 actual, string what) =>
            Assert.Less((expected - actual).magnitude, 1e-4f, $"{what}: expected {expected}, got {actual}");

        [Test]
        public void Smear_LiesFlat_AlongTheSlide_BehindItsMark()
        {
            var m = BloodTrailRenderer.SmearMatrix(new Vector3(2f, 0.5f, 3f), new Vector3(3f, 0f, 0f), Vector3.up, 2f, 1.6f, 0.25f, 0.02f);
            Near(new Vector3(2f - 0.4f, 0.52f, 3f), m.GetColumn(3), "centre: a quarter of its length behind the mark, lifted");
            Assert.AreEqual(1.6f, m.GetColumn(2).magnitude, 1e-4f, "length along the slide");
            Near(Vector3.right, ((Vector3)m.GetColumn(2)).normalized, "its length runs along the slide");
            Near(Vector3.up, m.GetColumn(1), "flat on the ground");
            Assert.AreEqual(2f, m.GetColumn(0).magnitude, 1e-4f, "width across the slide");
        }

        [Test]
        public void Smear_FollowsTheSlope_AndSurvivesMissingInputs()
        {
            Vector3 up = new Vector3(0f, Mathf.Cos(20f * Mathf.Deg2Rad), -Mathf.Sin(20f * Mathf.Deg2Rad)); // a ramp rising along +z
            var pos = new Vector3(0f, 1f, 0f);
            var m = BloodTrailRenderer.SmearMatrix(pos, Vector3.forward * 9f, up, 2f, 1.6f, 0.25f, 0.02f);
            Vector3 fwd = ((Vector3)m.GetColumn(2)).normalized;
            Assert.AreEqual(0f, Vector3.Dot(fwd, up), 1e-4f, "the smear lies in the slope");
            Assert.Greater(fwd.z, 0.9f, "still along the slide");
            Assert.Greater(fwd.y, 0.3f, "tilted up the ramp");
            Near(up, ((Vector3)m.GetColumn(1)).normalized, "its up is the ground normal");
            Assert.AreEqual(0.02f, Vector3.Dot((Vector3)m.GetColumn(3) - pos, up), 1e-4f, "on the slope plane, lifted");

            var flat = BloodTrailRenderer.SmearMatrix(pos, Vector3.forward, Vector3.zero, 2f, 1.6f, 0.25f, 0.02f);
            Near(Vector3.up, flat.GetColumn(1), "no normal: lies flat");
            var still = BloodTrailRenderer.SmearMatrix(pos, Vector3.zero, Vector3.up, 2f, 1.6f, 0.25f, 0.02f);
            for (int c = 0; c < 4; c++)
            for (int r = 0; r < 4; r++)
                Assert.IsFalse(float.IsNaN(still[r, c]) || float.IsInfinity(still[r, c]), "no direction: still a valid smear");
            Assert.AreEqual(1.6f, still.GetColumn(2).magnitude, 1e-4f);
        }

        [Test]
        public void ShapeOf_UsesEveryShape_BothWays_SameAsThePreview()
        {
            // PixelSrc/blood_smears.py's shape_of for marks 0..10 (the preview): keep the two hashes in step
            int[] cells = { 0, 1, 3, 0, 2, 1, 0, 2, 2, 1, 0 };
            bool[] mirrored = { false, false, true, true, true, true, true, false, true, false, true };
            for (int s = 0; s < cells.Length; s++)
            {
                Assert.AreEqual(cells[s], BloodTrailRenderer.ShapeOf(s, out bool mir), "mark " + s);
                Assert.AreEqual(mirrored[s], mir, "mark " + s + " mirrored");
            }
            var seen = new int[BloodTrailRenderer.Shapes * 2];
            for (int s = 0; s < 256; s++)
            {
                int cell = BloodTrailRenderer.ShapeOf(s, out bool mir);
                Assert.That(cell, Is.InRange(0, BloodTrailRenderer.Shapes - 1));
                seen[cell * 2 + (mir ? 1 : 0)]++;
            }
            foreach (int n in seen) Assert.Greater(n, 10, "every shape, both ways, turns up");
        }

        [Test]
        public void Setup_AtlasIsData_MaterialIsShared_PlayerCarriesTheRenderer()
        {
            var imp = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            Assert.IsNotNull(imp, "run python3 PixelSrc/blood_smears.py");
            Assert.IsFalse(imp.sRGBTexture, "the atlas is data (thickness, glint, noise): linear");
            Assert.IsFalse(imp.mipmapEnabled);
            Assert.AreEqual(FilterMode.Point, imp.filterMode, "crisp texels");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Assert.AreEqual(64, tex.width);
            Assert.AreEqual(64, tex.height);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(mat, "run Gladiator/Setup/Combat/Blood Trail");
            Assert.AreEqual("Gladiator/BloodSmear", mat.shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(mat.shader), "the shader compiles");
            Assert.AreEqual(tex, mat.GetTexture("_SmearTex"));
            float cellsPerSide = mat.GetFloat("_Cells");
            Assert.AreEqual(BloodTrailRenderer.Shapes, Mathf.RoundToInt(cellsPerSide * cellsPerSide), "the renderer's shapes are the atlas's cells");
            Assert.IsTrue(mat.enableInstancing);

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(player.GetComponent<BoonEffects>());
            var r = player.GetComponent<BloodTrailRenderer>();
            Assert.IsNotNull(r, "the player prefab carries the blood trail renderer");
            Assert.AreEqual(mat, r.material, "with the one shared material");
        }
    }
}
