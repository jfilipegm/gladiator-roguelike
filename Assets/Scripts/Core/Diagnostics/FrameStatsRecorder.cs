using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>One recorded frame. Times in milliseconds.</summary>
    public struct FrameSample
    {
        public float Time;          // unscaled seconds since recording started
        public float FrameMs;       // unscaled delta time
        public float MainThreadMs;  // profiler "Main Thread" (dev builds / editor; -1 if unavailable)
        public float GameplayMs;    // GameLoop marker: all centrally ticked systems
        public long GcBytes;        // GC Allocated In Frame (whole main thread — includes Editor noise in Play mode)
        public long GameplayHeap;   // managed-heap growth inside GameLoop ticks (page-granular, 0 = allocation-free)
        public long ScriptHeap;     // managed-heap growth from GameLoop start to end of LateUpdate (all game scripts)
        public int GcCount;         // GC Allocation In Frame Count
        public int Enemies, Projectiles, Pickups;
        public float TimeScale;
    }

    /// <summary>Summary over the recorded frames after the warm-up window.</summary>
    public struct FrameStatsSummary
    {
        public int Frames;
        public float Seconds;
        public float AvgFrameMs, P50FrameMs, P95FrameMs, P99FrameMs, MaxFrameMs;
        public float AvgMainMs, P95MainMs;
        public float AvgGameplayMs, P95GameplayMs, MaxGameplayMs;
        public float AvgFps;
        public long TotalGcBytes;
        public int FramesWithGc;
        public float AvgGcBytesPerFrame;
        public int MaxEnemies;
        public long TotalGameplayHeap, TotalScriptHeap;
        public int FramesWithGameplayHeap, FramesWithScriptHeap;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "frames={0} secs={1:0.0} fps(avg)={2:0.0} frameMs avg/p50/p95/p99/max={3:0.00}/{4:0.00}/{5:0.00}/{6:0.00}/{7:0.00} " +
            "mainMs avg/p95={8:0.00}/{9:0.00} gameplayMs avg/p95/max={10:0.000}/{11:0.000}/{12:0.000} " +
            "gc(main thread) totalB={13} framesWithGc={14} avgB/frame={15:0.0} | heapGrowth gameplay={17}B/{18}fr scripts={19}B/{20}fr | maxEnemies={16}",
            Frames, Seconds, AvgFps, AvgFrameMs, P50FrameMs, P95FrameMs, P99FrameMs, MaxFrameMs,
            AvgMainMs, P95MainMs, AvgGameplayMs, P95GameplayMs, MaxGameplayMs,
            TotalGcBytes, FramesWithGc, AvgGcBytesPerFrame, MaxEnemies,
            TotalGameplayHeap, FramesWithGameplayHeap, TotalScriptHeap, FramesWithScriptHeap);
    }

    /// <summary>
    /// Records per-frame CPU time, gameplay time, GC allocation and entity counts with ProfilerRecorder into a
    /// preallocated buffer (no per-frame allocation), then writes a CSV + summary on Stop/disable.
    /// </summary>
    [DefaultExecutionOrder(10000)] // LateUpdate last, so the script heap window covers every other script
    public sealed class FrameStatsRecorder : MonoBehaviour
    {
        [Tooltip("Max frames kept (ring stops recording when full).")]
        public int capacity = 120 * 60 * 15;
        [Tooltip("Seconds ignored by the summary (loading hitches, pool warm-up).")]
        public float warmupSeconds = 3f;
        [Tooltip("File name prefix; empty = scene name.")]
        public string label = "";
        public bool recordOnEnable = true;

        FrameSample[] _samples;
        int _count;
        float _startUnscaled;
        bool _recording;
        ProfilerRecorder _main, _gameplay, _gcBytes, _gcCount;
        // -rawProfile <startSeconds> [-rawFrames N]: write a Unity Profiler capture (.raw) of N frames for Tools/rawreport.
        float _rawStart = -1f;
        int _rawLeft;
        bool _rawOn;

        public int Count => _count;
        public bool IsRecording => _recording;
        public string LastCsvPath { get; private set; }
        public FrameStatsSummary LastSummary { get; private set; }

        void OnEnable()
        {
            if (recordOnEnable) Begin();
        }

        void OnDisable()
        {
            if (_recording) End();
        }

        public void Begin()
        {
            _samples ??= new FrameSample[capacity];
            _count = 0;
            _startUnscaled = UnityEngine.Time.unscaledTime;
            _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _gameplay = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, GameLoop.GameplayMarkerName, 1);
            _gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            _gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
            _recording = true;
            _rawStart = CommandLine.GetFloat("-rawProfile", -1f);
        }

        void UpdateRawCapture(float t)
        {
            if (_rawStart >= 0f && !_rawOn && t >= _rawStart)
            {
                string dir = OutputDirectory();
                Directory.CreateDirectory(dir);
                UnityEngine.Profiling.Profiler.logFile = Path.Combine(dir, (string.IsNullOrEmpty(label) ? "capture" : label) + ".raw");
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                UnityEngine.Profiling.Profiler.enabled = true;
                _rawOn = true;
                _rawLeft = CommandLine.GetInt("-rawFrames", 1500);
                Debug.Log("[FrameStats] raw profiler capture → " + UnityEngine.Profiling.Profiler.logFile);
            }
            else if (_rawOn && --_rawLeft <= 0)
            {
                UnityEngine.Profiling.Profiler.enabled = false;
                UnityEngine.Profiling.Profiler.enableBinaryLog = false;
                UnityEngine.Profiling.Profiler.logFile = "";
                _rawOn = false;
                _rawStart = -1f;
                Debug.Log("[FrameStats] raw profiler capture done");
            }
        }

        void LateUpdate()
        {
            if (!_recording || _count >= _samples.Length) return;
            if (_rawStart >= 0f || _rawOn) UpdateRawCapture(UnityEngine.Time.unscaledTime - _startUnscaled);
            ref var s = ref _samples[_count++];
            s.Time = UnityEngine.Time.unscaledTime - _startUnscaled;
            s.FrameMs = UnityEngine.Time.unscaledDeltaTime * 1000f;
            s.MainThreadMs = _main.Valid ? _main.LastValue * 1e-6f : -1f;
            s.GameplayMs = _gameplay.Valid ? _gameplay.LastValue * 1e-6f : -1f;
            s.GcBytes = _gcBytes.Valid ? _gcBytes.LastValue : -1;
            s.GcCount = _gcCount.Valid ? (int)_gcCount.LastValue : -1;
            s.GameplayHeap = GameLoop.LastGameplayHeapGrowth;
            long heapNow = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            s.ScriptHeap = heapNow > GameLoop.FrameHeapStart ? heapNow - GameLoop.FrameHeapStart : 0;
            s.Enemies = DevCounters.Enemies;
            s.Projectiles = DevCounters.Projectiles;
            s.Pickups = DevCounters.Pickups;
            s.TimeScale = UnityEngine.Time.timeScale;
        }

        /// <summary>Stops recording, writes the CSV and returns the summary.</summary>
        public FrameStatsSummary End()
        {
            _recording = false;
            _main.Dispose(); _gameplay.Dispose(); _gcBytes.Dispose(); _gcCount.Dispose();
            var summary = Summarize(_samples, _count, warmupSeconds);
            LastSummary = summary;
            try { LastCsvPath = WriteCsv(summary); }
            catch (Exception e) { Debug.LogWarning("[FrameStats] CSV write failed: " + e.Message); }
            Debug.Log("[FrameStats] " + summary + (LastCsvPath != null ? "\n  csv: " + LastCsvPath : ""));
            return summary;
        }

        public static string OutputDirectory()
        {
            var custom = CommandLine.Get("-profileDir");
            if (!string.IsNullOrEmpty(custom)) return custom;
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Profiling"));
#else
            return Path.Combine(Application.persistentDataPath, "Profiling");
#endif
        }

        string WriteCsv(FrameStatsSummary summary)
        {
            var dir = OutputDirectory();
            Directory.CreateDirectory(dir);
            var name = string.IsNullOrEmpty(label) ? gameObject.scene.name : label;
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, $"{stamp}_{name}.csv");
            var sb = new StringBuilder(_count * 64 + 256);
            sb.AppendLine("time_s,frame_ms,main_ms,gameplay_ms,gc_bytes,gc_count,gameplay_heap,script_heap,enemies,projectiles,pickups,timescale");
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < _count; i++)
            {
                ref var s = ref _samples[i];
                sb.Append(s.Time.ToString("0.000", inv)).Append(',')
                  .Append(s.FrameMs.ToString("0.000", inv)).Append(',')
                  .Append(s.MainThreadMs.ToString("0.000", inv)).Append(',')
                  .Append(s.GameplayMs.ToString("0.0000", inv)).Append(',')
                  .Append(s.GcBytes).Append(',').Append(s.GcCount).Append(',')
                  .Append(s.GameplayHeap).Append(',').Append(s.ScriptHeap).Append(',')
                  .Append(s.Enemies).Append(',').Append(s.Projectiles).Append(',').Append(s.Pickups).Append(',')
                  .Append(s.TimeScale.ToString("0.00", inv)).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
            File.WriteAllText(Path.ChangeExtension(path, ".summary.txt"), summary.ToString() + "\n");
            return path;
        }

        /// <summary>Pure function so it can be unit-tested.</summary>
        public static FrameStatsSummary Summarize(FrameSample[] samples, int count, float warmupSeconds)
        {
            var r = new FrameStatsSummary();
            int start = 0;
            while (start < count && samples[start].Time < warmupSeconds) start++;
            int n = count - start;
            if (n <= 0) return r;

            var frame = new float[n];
            var main = new float[n];
            var gameplay = new float[n];
            double sumFrame = 0, sumMain = 0, sumGameplay = 0;
            int mainN = 0, gpN = 0;
            for (int i = 0; i < n; i++)
            {
                ref var s = ref samples[start + i];
                frame[i] = s.FrameMs; sumFrame += s.FrameMs;
                main[i] = s.MainThreadMs >= 0 ? s.MainThreadMs : 0f;
                if (s.MainThreadMs >= 0) { sumMain += s.MainThreadMs; mainN++; }
                gameplay[i] = s.GameplayMs >= 0 ? s.GameplayMs : 0f;
                if (s.GameplayMs >= 0) { sumGameplay += s.GameplayMs; gpN++; }
                if (s.GcBytes > 0) { r.TotalGcBytes += s.GcBytes; r.FramesWithGc++; }
                if (s.Enemies > r.MaxEnemies) r.MaxEnemies = s.Enemies;
                if (s.GameplayHeap > 0) { r.TotalGameplayHeap += s.GameplayHeap; r.FramesWithGameplayHeap++; }
                if (s.ScriptHeap > 0) { r.TotalScriptHeap += s.ScriptHeap; r.FramesWithScriptHeap++; }
            }
            r.Frames = n;
            r.Seconds = samples[count - 1].Time - samples[start].Time;
            r.AvgFrameMs = (float)(sumFrame / n);
            r.AvgFps = r.AvgFrameMs > 0 ? 1000f / r.AvgFrameMs : 0f;
            r.AvgMainMs = mainN > 0 ? (float)(sumMain / mainN) : -1f;
            r.AvgGameplayMs = gpN > 0 ? (float)(sumGameplay / gpN) : -1f;
            r.AvgGcBytesPerFrame = (float)r.TotalGcBytes / n;
            Array.Sort(frame); Array.Sort(main); Array.Sort(gameplay);
            r.P50FrameMs = Percentile(frame, 0.50f);
            r.P95FrameMs = Percentile(frame, 0.95f);
            r.P99FrameMs = Percentile(frame, 0.99f);
            r.MaxFrameMs = frame[n - 1];
            r.P95MainMs = mainN > 0 ? Percentile(main, 0.95f) : -1f;
            r.P95GameplayMs = gpN > 0 ? Percentile(gameplay, 0.95f) : -1f;
            r.MaxGameplayMs = gpN > 0 ? gameplay[n - 1] : -1f;
            return r;
        }

        /// <summary>Nearest-rank percentile of an ascending-sorted array.</summary>
        public static float Percentile(float[] sorted, float p)
        {
            if (sorted.Length == 0) return 0f;
            int rank = Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1);
            return sorted[rank];
        }
    }
}
