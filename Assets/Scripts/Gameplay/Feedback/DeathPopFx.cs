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

        /// <summary>A status tick on an enemy: two small motes rising around its body (Burn gold, Bleed crimson, Shock cyan).</summary>
        public void StatusPuff(Vector3 position, float radius, Color color)
        {
            for (int k = 0; k < 2; k++)
            {
                Vector3 off = new Vector3(GameRandom.Range(-1f, 1f), GameRandom.Range(-0.4f, 0.4f), GameRandom.Range(-1f, 1f)) * radius;
                _ep.position = position + off;
                _ep.velocity = new Vector3(off.x * 0.6f, GameRandom.Range(2.6f, 4f), off.z * 0.6f);
                _ep.startSize = GameRandom.Range(0.15f, 0.24f);
                _ep.startColor = color;
                _ep.startLifetime = GameRandom.Range(0.3f, 0.5f);
                _ps.Emit(_ep, 1);
            }
        }

        /// <summary>Radial burst (level-up, pickups of note). Player colours: cyan/white/gold.</summary>
        public void Burst(Vector3 position, int count, float speed, float size, Color a, Color b)
        {
            for (int k = 0; k < count; k++)
            {
                float ang = k * Mathf.PI * 2f / count + GameRandom.Range(-0.1f, 0.1f);
                Vector3 dir = new Vector3(Mathf.Cos(ang), GameRandom.Range(0.2f, 1.1f), Mathf.Sin(ang)).normalized;
                _ep.position = position;
                _ep.velocity = dir * speed * GameRandom.Range(0.7f, 1.2f);
                _ep.startSize = size * GameRandom.Range(0.7f, 1.3f);
                _ep.startColor = (k & 1) == 0 ? a : b;
                _ep.startLifetime = GameRandom.Range(0.45f, 0.75f);
                _ps.Emit(_ep, 1);
            }
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
