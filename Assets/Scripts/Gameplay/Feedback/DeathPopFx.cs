using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// One ParticleSystem for every death pop in the arena (manual Emit, world space) — one draw call, pooled by design.
    /// Configured in code so nothing depends on hand-edited prefab YAML.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class DeathPopFx : MonoBehaviour
    {
        public Material particleMaterial;
        public int maxParticles = 3000;
        ParticleSystem _ps;
        ParticleSystem.EmitParams _ep;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.gravityModifier = 2.2f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = particleMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _ps.Play();
        }

        public void Emit(Vector3 position, Vector3 pushDir, HitFeedbackConfig c)
        {
            int n = c.deathPopParticles;
            for (int k = 0; k < n; k++)
            {
                Vector3 dir = new Vector3(GameRandom.Range(-1f, 1f), GameRandom.Range(0.4f, 1.2f), GameRandom.Range(-1f, 1f)).normalized;
                dir += pushDir * 0.6f;
                _ep.position = position + dir * 0.15f;
                _ep.velocity = dir * c.deathPopSpeed * GameRandom.Range(0.6f, 1.2f);
                _ep.startSize = c.deathPopSize * GameRandom.Range(0.7f, 1.3f);
                _ep.startColor = k % 3 == 0 ? c.deathPopAccent : c.deathPopColor;
                _ep.startLifetime = GameRandom.Range(0.35f, 0.6f);
                _ps.Emit(_ep, 1);
            }
        }
    }
}
