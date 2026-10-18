using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Pooled melee slash arcs (FX_SlashArc mesh + Gladiator/FxSlash). A handful exist at once, so a
    /// MaterialPropertyBlock for progress/arc is fine here (not a hot mesh, CLAUDE.md §4).
    /// </summary>
    public sealed class SlashFx : MonoBehaviour
    {
        public Mesh arcMesh;
        public Material material;
        public int poolSize = 6;

        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int ArcId = Shader.PropertyToID("_ArcFrac");

        struct Slot { public Transform T; public MeshRenderer R; public float T0, Duration; public bool Busy; public float Arc; }
        Slot[] _slots;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _slots = new Slot[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Slash" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = arcMesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _slots[i] = new Slot { T = go.transform, R = r };
            }
        }

        /// <summary>Arc mesh spans 180°; arcDegrees clips it. Sweeps right → left over duration.</summary>
        public void Play(Vector3 origin, Vector3 dir, float radius, float arcDegrees, float duration)
        {
            int idx = -1;
            for (int i = 0; i < _slots.Length; i++) if (!_slots[i].Busy) { idx = i; break; }
            if (idx < 0) idx = 0;
            ref var s = ref _slots[idx];
            s.Busy = true;
            s.T0 = Time.time;
            s.Duration = Mathf.Max(0.05f, duration * 1.6f);
            s.Arc = Mathf.Clamp01(arcDegrees / 180f);
            s.T.gameObject.SetActive(true);
            s.T.SetPositionAndRotation(origin, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)));
            s.T.localScale = Vector3.one * radius;
            Apply(ref s, 0f);
        }

        void Apply(ref Slot s, float t)
        {
            _mpb.SetFloat(ProgressId, t);
            _mpb.SetFloat(ArcId, s.Arc);
            s.R.SetPropertyBlock(_mpb);
        }

        void LateUpdate()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Busy) continue;
                float t = (Time.time - s.T0) / s.Duration;
                if (t >= 1f) { s.Busy = false; s.T.gameObject.SetActive(false); continue; }
                Apply(ref s, t);
            }
        }
    }
}
