using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena's gates (M3.2): where swarms and elites enter. The Director asks for a gate away from the player and
    /// spawns just inside its threshold; the doors swing open while enemies come through. Central tick, no GC.
    /// </summary>
    public sealed class ArenaGates : MonoBehaviour, ITickable
    {
        public static ArenaGates Instance { get; private set; }

        [Tooltip("Gate model roots, in layout order (each has DoorL / DoorR children).")]
        public Transform[] gates = System.Array.Empty<Transform>();
        [Tooltip("How far inside the gate line enemies appear (m, min..max).")]
        public Vector2 spawnDepth = new Vector2(0.8f, 3.2f);
        [Tooltip("Sideways spread across the opening (m).")]
        public float spawnSpread = 1.3f;
        public float doorOpenAngle = 100f;
        public float doorSpeed = 260f;

        Transform[] _doorL, _doorR;
        float[] _openUntil, _angle;
        float _clock;

        public int Count => gates.Length;

        void Awake()
        {
            Instance = this;
            int n = gates.Length;
            _doorL = new Transform[n]; _doorR = new Transform[n];
            _openUntil = new float[n]; _angle = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (gates[i] == null) continue;
                _doorL[i] = FindDeep(gates[i], "DoorL");
                _doorR[i] = FindDeep(gates[i], "DoorR");
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Director, 50);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>Into the arena from gate i (its model's +Z).</summary>
        public Vector3 Inward(int i) => gates[i].forward;
        public Vector3 Position(int i) => gates[i].position;

        /// <summary>A gate at least `minDistance` from `player`, preferring the one behind the camera's view (-1 if none).</summary>
        public int Pick(Vector3 player, float minDistance)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] == null) continue;
                Vector3 d = gates[i].position - player;
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist < minDistance) continue;
                float score = dist + GameRandom.Range(0f, 8f); // farther is likelier, never fixed
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>A spawn point just inside gate i, spread across its opening.</summary>
        public Vector3 SpawnPoint(int i)
        {
            var g = gates[i];
            return g.position + g.forward * GameRandom.Range(spawnDepth.x, spawnDepth.y) + g.right * GameRandom.Range(-spawnSpread, spawnSpread);
        }

        /// <summary>Swings gate i open for `seconds`.</summary>
        public void Open(int i, float seconds)
        {
            if (!IsOpen(i)) SfxPlayer.Play(SfxId.Gate, Position(i));
            if (i < 0 || i >= gates.Length) return;
            _openUntil[i] = Mathf.Max(_openUntil[i], _clock + seconds);
        }

        public bool IsOpen(int i) => _angle[i] > 1f;

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            _clock += dt;
            for (int i = 0; i < gates.Length; i++)
            {
                float target = _clock < _openUntil[i] ? doorOpenAngle : 0f;
                float a = Mathf.MoveTowards(_angle[i], target, doorSpeed * dt);
                if (Mathf.Approximately(a, _angle[i])) continue;
                _angle[i] = a;
                // doors swing into the arena: each turns toward its own side
                if (_doorL[i] != null) _doorL[i].localRotation = Quaternion.Euler(0f, a * Mathf.Sign(_doorL[i].localPosition.x), 0f);
                if (_doorR[i] != null) _doorR[i].localRotation = Quaternion.Euler(0f, a * Mathf.Sign(_doorR[i].localPosition.x), 0f);
            }
        }
    }
}
