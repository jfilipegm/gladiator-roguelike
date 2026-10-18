using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Reads a player profiler capture (.raw from a development build: -profiler-enable -profiler-log-file x.raw, or
    /// -rawProfile) and writes a text report next to it: the slowest frames with their top markers, and every GC.Alloc
    /// grouped by caller path. Compiled (not eval'd) because captures are large; the report lands in x.raw.report.txt.
    /// </summary>
    public static class ProfilerReports
    {
        public static string Analyze(string rawPath, int slowFrames = 3, int depth = 5, float minMs = 0.5f)
        {
            ProfilerDriver.ClearAllFrames();
            if (!ProfilerDriver.LoadProfile(rawPath, false)) return "could not load " + rawPath;
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var frameMs = new List<(int frame, float ms, double gc)>();
            var gcTotals = new Dictionary<string, (double bytes, int frames)>();
            var kids = new List<int>();

            for (int f = first; f <= last && f >= 0; f++)
            {
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (view == null || !view.valid) continue;
                    double gcFrame = 0;
                    var stack = new Stack<(int id, string path)>();
                    stack.Push((view.GetRootItemID(), ""));
                    while (stack.Count > 0)
                    {
                        var (id, path) = stack.Pop();
                        kids.Clear();
                        view.GetItemChildren(id, kids);
                        foreach (var k in kids)
                        {
                            string name = view.GetItemName(k);
                            if (name == "GC.Alloc")
                            {
                                double bytes = view.GetItemColumnDataAsDouble(k, HierarchyFrameDataView.columnGcMemory);
                                gcFrame += bytes;
                                var parts = path.Split(new[] { " > " }, System.StringSplitOptions.None);
                                string key = string.Join(" > ", parts, System.Math.Max(0, parts.Length - 4), System.Math.Min(4, parts.Length));
                                gcTotals.TryGetValue(key, out var t);
                                gcTotals[key] = (t.bytes + bytes, t.frames + 1);
                            }
                            else stack.Push((k, path.Length == 0 ? name : path + " > " + name));
                        }
                    }
                    frameMs.Add((f, view.frameTimeMs, gcFrame));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"capture {Path.GetFileName(rawPath)}: frames {first}..{last} ({frameMs.Count} read)");
            var sorted = new List<(int frame, float ms, double gc)>(frameMs);
            sorted.Sort((a, b) => b.ms.CompareTo(a.ms));
            for (int s = 0; s < System.Math.Min(slowFrames, sorted.Count); s++)
            {
                var (frame, ms, gc) = sorted[s];
                sb.AppendLine().AppendLine($"== slow frame {frame}: {ms:0.00} ms, GC {gc:0} B");
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           HierarchyFrameDataView.columnTotalTime, false))
                    Dump(view, view.GetRootItemID(), 0, depth, minMs, sb, kids);
            }

            sb.AppendLine().AppendLine("== GC.Alloc by caller (all frames)");
            var gcList = new List<KeyValuePair<string, (double bytes, int frames)>>(gcTotals);
            gcList.Sort((a, b) => b.Value.bytes.CompareTo(a.Value.bytes));
            foreach (var kv in gcList) sb.AppendLine($"{kv.Value.bytes,10:0} B in {kv.Value.frames,5} frames :: {kv.Key}");

            sb.AppendLine().AppendLine("== frames with GC (frame, ms, bytes)");
            int listed = 0;
            foreach (var fr in frameMs)
                if (fr.gc > 0 && listed++ < 60) sb.AppendLine($"{fr.frame} {fr.ms:0.00}ms {fr.gc:0}B");

            string report = sb.ToString();
            File.WriteAllText(rawPath + ".report.txt", report);
            return report;
        }

        static void Dump(HierarchyFrameDataView view, int id, int level, int maxDepth, float minMs, StringBuilder sb, List<int> scratch)
        {
            if (level >= maxDepth) return;
            var kids = new List<int>();
            view.GetItemChildren(id, kids);
            kids.Sort((a, b) => view.GetItemColumnDataAsFloat(b, HierarchyFrameDataView.columnTotalTime)
                .CompareTo(view.GetItemColumnDataAsFloat(a, HierarchyFrameDataView.columnTotalTime)));
            foreach (var k in kids)
            {
                float ms = view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnTotalTime);
                if (ms < minMs) continue;
                float gc = view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnGcMemory);
                sb.Append(' ', level * 2).AppendLine($"{ms,8:0.00} ms  {view.GetItemName(k)}{(gc > 0 ? $"  [GC {gc:0} B]" : "")}");
                Dump(view, k, level + 1, maxDepth, minMs, sb, scratch);
            }
        }
    }
}
