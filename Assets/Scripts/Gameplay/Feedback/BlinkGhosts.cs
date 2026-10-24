using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Note 35 (the producer: the Thunder Champion's blink "is sudden, I at first thought it was a bug"): after-images of
    /// his silhouette along a blink's path, appearing from the start to the landing in a few hundredths of a second and
    /// fading from the start first, so the trail pulls in behind him; then a pale shell that swells and fades where he
    /// lands. Each part of his rigid-part model is snapshotted as it stands at the landing and redrawn at points along
    /// the path with the additive Gladiator/FxGhost shader (cool white / cyan, CLAUDE.md §5).
    /// A few instanced draws for a third of a second every few seconds: one shared material, per-instance colour through
    /// an MPB array (an FX system, not a hot mesh, as TelegraphRenderer). A plain class owned and ticked by
    /// LightningDash: the parts are gathered once (at spawn), everything else is preallocated (no allocation per blink).
    /// </summary>
    public sealed class BlinkGhosts
    {
        /// <summary>After-images at once: up to MaxGhosts - 1 along the path + the landing shell.</summary>
        public const int MaxGhosts = 8;
        const int MaxParts = 32;
        const float ShellHeight = 0.8f; // the landing shell swells about this point above his feet
        static readonly int ColorId = Shader.PropertyToID("_GColor");

        readonly Transform _model;
        readonly MeshFilter[] _filters = new MeshFilter[MaxParts];
        readonly Renderer[] _renderers = new Renderer[MaxParts];
        readonly Matrix4x4[] _pose = new Matrix4x4[MaxParts];
        readonly bool[] _posed = new bool[MaxParts];
        readonly Matrix4x4[][] _matrices = new Matrix4x4[MaxParts][];
        readonly Vector4[] _colors = new Vector4[MaxGhosts];
        readonly Vector3[] _offset = new Vector3[MaxGhosts];
        readonly float[] _age = new float[MaxGhosts], _life = new float[MaxGhosts], _strength = new float[MaxGhosts], _grow = new float[MaxGhosts];
        readonly Color[] _tint = new Color[MaxGhosts];
        readonly MaterialPropertyBlock _mpb;
        readonly bool _on;
        RenderParams _rp;
        int _parts, _count;
        bool _gathered;
        Vector3 _centre;

        /// <summary>After-images alive (tests).</summary>
        public int Alive => _count;
        /// <summary>Model parts each after-image is drawn from (tests).</summary>
        public int Parts => _parts;

        public BlinkGhosts(Material material, Transform model)
        {
            _model = model;
            _on = material != null && model != null;
            if (_on && !material.enableInstancing)
            {
                Debug.LogWarning("[BlinkGhosts] " + material.name + " has GPU instancing off: no after-images (run Gladiator/Setup/Gladiators/Blink FX)");
                _on = false;
            }
            for (int p = 0; p < MaxParts; p++) _matrices[p] = new Matrix4x4[MaxGhosts];
            _mpb = new MaterialPropertyBlock();
            _mpb.SetVectorArray(ColorId, _colors); // fixes the array's size once
            if (!_on) return;
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                matProps = _mpb,
            };
            Gather();
        }

        /// <summary>Every mesh part under the model, hidden ones too (a thrown weapon's part comes back); once.</summary>
        void Gather()
        {
            _gathered = true;
            var found = new List<MeshFilter>(MaxParts);
            _model.GetComponentsInChildren(true, found);
            _parts = 0;
            for (int i = 0; i < found.Count && _parts < MaxParts; i++)
            {
                var r = found[i].GetComponent<MeshRenderer>();
                if (r == null) continue;
                _filters[_parts] = found[i];
                _renderers[_parts] = r;
                _parts++;
            }
        }

        /// <summary>
        /// A blink from `from` to `to` (he stands at `to` now): after-images every `t.ghostSpacing` metres from the start,
        /// the landing shell, all from his pose this frame. A blink too short for one after-image leaves only the shell.
        /// </summary>
        public void Spawn(Vector3 from, Vector3 to, LightningDashTrait t)
        {
            if (!_on || t == null) return;
            if (!_gathered) Gather();
            for (int p = 0; p < _parts; p++)
            {
                var f = _filters[p];
                var r = _renderers[p];
                _posed[p] = f != null && r != null && r.enabled && r.gameObject.activeInHierarchy && f.sharedMesh != null;
                if (_posed[p]) _pose[p] = f.transform.localToWorldMatrix;
            }
            _count = 0;
            _centre = to + Vector3.up * ShellHeight;
            Vector3 back = from - to;
            float len = new Vector3(back.x, 0f, back.z).magnitude;
            float spacing = Mathf.Max(0.2f, t.ghostSpacing);
            int n = len < spacing * 0.5f ? 0 : Mathf.Clamp(Mathf.RoundToInt(len / spacing), 1, MaxGhosts - 1);
            for (int k = 0; k < n; k++)
            {
                float f = (float)k / n; // 0 at the start, (n - 1) / n near the landing
                Add(back * (1f - f), f * t.ghostSweep, t.ghostLife * (0.5f + 0.5f * f),
                    Color.Lerp(t.ghostColorStart, t.ghostColorEnd, f), 0.55f + 0.45f * f, 0f);
            }
            if (t.landingFlash > 0f) Add(Vector3.zero, t.ghostSweep, t.landingFlash, t.landingFlashColor, 1f, t.landingFlashGrow);
        }

        void Add(Vector3 offset, float delay, float life, Color tint, float strength, float grow)
        {
            if (_count >= MaxGhosts || life <= 0f) return;
            _offset[_count] = offset;
            _age[_count] = -Mathf.Max(0f, delay);
            _life[_count] = life;
            _tint[_count] = tint;
            _strength[_count] = strength;
            _grow[_count] = grow;
            _count++;
        }

        /// <summary>Ages and draws the after-images (call every frame; `dt` 0 while paused keeps them as they are).</summary>
        public void Tick(float dt)
        {
            if (_count == 0) return;
            for (int g = _count - 1; g >= 0; g--)
            {
                _age[g] += dt;
                if (_age[g] < _life[g]) continue;
                int last = --_count;
                _offset[g] = _offset[last]; _age[g] = _age[last]; _life[g] = _life[last];
                _tint[g] = _tint[last]; _strength[g] = _strength[last]; _grow[g] = _grow[last];
            }
            if (_count == 0) return;
            for (int g = 0; g < _count; g++)
            {
                float a = 0f;
                if (_age[g] >= 0f)
                {
                    float k = 1f - _age[g] / _life[g];
                    a = k * k * _strength[g];
                }
                var c = _tint[g];
                _colors[g] = new Vector4(c.r, c.g, c.b, a);
            }
            _mpb.SetVectorArray(ColorId, _colors);
            for (int p = 0; p < _parts; p++)
            {
                if (!_posed[p]) continue;
                var m = _matrices[p];
                for (int g = 0; g < _count; g++)
                {
                    if (_grow[g] <= 0f)
                    {
                        m[g] = Matrix4x4.Translate(_offset[g]) * _pose[p];
                        continue;
                    }
                    float s = 1f + _grow[g] * Mathf.Clamp01(_age[g] / _life[g]);
                    m[g] = Matrix4x4.Translate(_offset[g] + _centre) * Matrix4x4.Scale(new Vector3(s, s, s)) * Matrix4x4.Translate(-_centre) * _pose[p];
                }
                var mesh = _filters[p] != null ? _filters[p].sharedMesh : null;
                if (mesh == null) continue;
                for (int sm = 0; sm < mesh.subMeshCount; sm++) Graphics.RenderMeshInstanced(_rp, mesh, sm, m, _count);
            }
        }

        /// <summary>Gone at once (death, the run's end).</summary>
        public void Clear() => _count = 0;
    }
}
