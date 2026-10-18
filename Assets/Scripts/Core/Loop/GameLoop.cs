using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Gladiator.Core
{
    /// <summary>Execution order of centrally ticked systems (lower runs first).</summary>
    public enum TickGroup
    {
        Input = 0,
        Player = 100,
        Director = 200,
        Enemies = 300,
        Weapons = 400,
        Projectiles = 500,
        Pickups = 600,
        Feedback = 700,
        Camera = 800,
        Late = 900,
    }

    public interface ITickable
    {
        /// <param name="dt">Scaled delta time (0 while paused).</param>
        /// <param name="unscaledDt">Real delta time.</param>
        void Tick(float dt, float unscaledDt);
    }

    /// <summary>
    /// The single Update() for gameplay. Systems register with a TickGroup; no per-entity Update.
    /// Iteration is allocation-free; registration happens at setup time.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameLoop : MonoBehaviour
    {
        struct Entry
        {
            public int Order;
            public ITickable System;
            public ProfilerMarker Marker;
            public long HeapGrowth;
            public int HeapFrames;
        }

        /// <summary>Debug: attribute managed-heap growth to each system (Tools: GameLoop.HeapReport()).</summary>
        public static bool TrackHeapPerSystem;

        static GameLoop _instance;
        readonly List<Entry> _entries = new List<Entry>(32);
        static readonly ProfilerMarker GameplayMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Gladiator.Gameplay");

        /// <summary>Name of the profiler marker wrapping all gameplay ticks (read by FrameStatsRecorder).</summary>
        public const string GameplayMarkerName = "Gladiator.Gameplay";

        /// <summary>Mono heap used size at the start of this frame's GameLoop.Update (FrameStatsRecorder diffs against it).</summary>
        public static long FrameHeapStart;
        /// <summary>Managed-heap growth across all gameplay ticks this frame (page-granular; 0 when allocation-free).</summary>
        public static long LastGameplayHeapGrowth;

        public static GameLoop Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameLoop>();
                    if (_instance == null && Application.isPlaying)
                        _instance = new GameObject("[GameLoop]").AddComponent<GameLoop>();
                }
                return _instance;
            }
        }

        public static void Register(ITickable system, TickGroup group, int subOrder = 0)
        {
            var loop = Instance;
            if (loop == null) return;
            int order = (int)group + subOrder;
            var name = system.GetType().Name;
            var entry = new Entry { Order = order, System = system, Marker = new ProfilerMarker(ProfilerCategory.Scripts, "Tick." + name) };
            int i = 0;
            while (i < loop._entries.Count && loop._entries[i].Order <= order) i++;
            loop._entries.Insert(i, entry);
        }

        public static void Unregister(ITickable system)
        {
            if (_instance == null) return;
            var list = _instance._entries;
            for (int i = list.Count - 1; i >= 0; i--)
                if (ReferenceEquals(list[i].System, system)) list.RemoveAt(i);
        }

        public static string HeapReport()
        {
            if (_instance == null) return "no loop";
            var sb = new System.Text.StringBuilder();
            foreach (var e in _instance._entries)
                sb.Append(e.System.GetType().Name).Append(": ").Append(e.HeapGrowth).Append("B/").Append(e.HeapFrames).Append("fr\n");
            return sb.ToString();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            long heap0 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            FrameHeapStart = heap0;
            TimeController.Update();
            float dt = UnityEngine.Time.deltaTime;
            float udt = UnityEngine.Time.unscaledDeltaTime;
            using (GameplayMarker.Auto())
            {
                // Index loop: systems may unregister during a tick (e.g. on death) — re-check bounds.
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    long h0 = TrackHeapPerSystem ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : 0;
                    e.Marker.Begin();
                    e.System.Tick(dt, udt);
                    e.Marker.End();
                    if (TrackHeapPerSystem && i < _entries.Count)
                    {
                        long d = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - h0;
                        if (d > 0) { e.HeapGrowth += d; e.HeapFrames++; _entries[i] = e; }
                    }
                }
            }
            long heap1 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            LastGameplayHeapGrowth = heap1 > heap0 ? heap1 - heap0 : 0;
        }
    }
}
