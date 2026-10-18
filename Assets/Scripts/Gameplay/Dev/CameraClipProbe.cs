using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Debug: counts frames where the main camera touches Environment colliders and which ones.</summary>
    [DefaultExecutionOrder(20000)]
    public sealed class CameraClipProbe : MonoBehaviour
    {
        public float radius = 0.1f;
        readonly Collider[] _hits = new Collider[8];
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly List<string> _samples = new List<string>();
        public int Frames, InsideFrames;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Frames++;
            int n = Physics.OverlapSphereNonAlloc(cam.transform.position, radius, _hits, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            if (n == 0) return;
            InsideFrames++;
            if (_samples.Count < 12 && Player.Main != null)
            {
                var pm = Player.Main.GetComponent<PlayerMotor>();
                _samples.Add($"cam={cam.transform.position:F2} player={Player.Main.transform.position:F2} state={pm.CurrentState} hit={_hits[0].name} closest={_hits[0].ClosestPoint(cam.transform.position):F2}");
            }
            for (int i = 0; i < n; i++)
            {
                var key = _hits[i].name + (_hits[i].transform.parent ? "<" + _hits[i].transform.parent.name : "") + " camY=" + cam.transform.position.y.ToString("F1");
                _counts.TryGetValue(key, out int c);
                _counts[key] = c + 1;
            }
        }

        public string Report()
        {
            var sb = new StringBuilder($"frames={Frames} inside={InsideFrames}\n");
            foreach (var kv in _counts) sb.AppendLine($"  {kv.Value}x {kv.Key}");
            foreach (var smp in _samples) sb.AppendLine("  sample " + smp);
            return sb.ToString();
        }
    }
}
