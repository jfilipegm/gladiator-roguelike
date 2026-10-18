using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Per-scene look: hemisphere ambient (read by Gladiator/PaletteLit), fog and skybox.
    /// Arenas set their own values (each arena has its own palette/mood); runs in edit mode so scenes look right.
    /// </summary>
    [ExecuteAlways]
    public sealed class EnvironmentLighting : MonoBehaviour
    {
        static readonly int AmbientSkyId = Shader.PropertyToID("_GLAmbientSky");
        static readonly int AmbientEquatorId = Shader.PropertyToID("_GLAmbientEquator");
        static readonly int AmbientGroundId = Shader.PropertyToID("_GLAmbientGround");

        [Header("Ambient (hemisphere)")]
        public Color ambientSky = new Color(0.60f, 0.72f, 0.90f);
        public Color ambientEquator = new Color(0.60f, 0.60f, 0.58f);
        public Color ambientGround = new Color(0.42f, 0.36f, 0.30f);
        [Range(0f, 2f)] public float ambientIntensity = 1f;

        [Header("Fog")]
        public bool fog = true;
        public Color fogColor = new Color(0.70f, 0.84f, 0.95f);
        public float fogStart = 30f;
        public float fogEnd = 110f;

        [Header("Sky")]
        public Material skybox;
        public Light sun;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            Shader.SetGlobalColor(AmbientSkyId, ambientSky * ambientIntensity);
            Shader.SetGlobalColor(AmbientEquatorId, ambientEquator * ambientIntensity);
            Shader.SetGlobalColor(AmbientGroundId, ambientGround * ambientIntensity);

            // Keep Unity's own ambient in sync so anything using SH (e.g. particles) matches.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * ambientIntensity;
            RenderSettings.ambientEquatorColor = ambientEquator * ambientIntensity;
            RenderSettings.ambientGroundColor = ambientGround * ambientIntensity;

            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (skybox != null) RenderSettings.skybox = skybox;
            if (sun != null) RenderSettings.sun = sun;
        }
    }
}
