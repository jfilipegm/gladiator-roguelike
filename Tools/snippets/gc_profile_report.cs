// Aggregates GC.Alloc samples on the main thread by their sample path over all recorded frames.
UnityEditorInternal.ProfilerDriver.enabled = false;
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
var totals = new System.Collections.Generic.Dictionary<string, double>();
var counts = new System.Collections.Generic.Dictionary<string, int>();
var kids = new System.Collections.Generic.List<int>();
int frames = 0;
for (int f = first; f <= last && f >= 0; f++)
{
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory, false))
    {
        if (view == null || !view.valid) continue;
        frames++;
        var stack = new System.Collections.Generic.Stack<(int id, string path)>();
        stack.Push((view.GetRootItemID(), ""));
        while (stack.Count > 0)
        {
            var (id, path) = stack.Pop();
            kids.Clear();
            view.GetItemChildren(id, kids);
            foreach (var k in kids)
            {
                string name = view.GetItemName(k);
                string p = path.Length == 0 ? name : path + " > " + name;
                if (name == "GC.Alloc")
                {
                    double bytes = view.GetItemColumnDataAsDouble(k, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory);
                    // keep the last 4 path elements: enough to identify the caller
                    var parts = path.Split(new[] { " > " }, System.StringSplitOptions.None);
                    string key = string.Join(" > ", parts, System.Math.Max(0, parts.Length - 4), System.Math.Min(4, parts.Length));
                    totals.TryGetValue(key, out var t); totals[key] = t + bytes;
                    counts.TryGetValue(key, out var c); counts[key] = c + 1;
                }
                else stack.Push((k, p));
            }
        }
    }
}
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(totals);
list.Sort((a, b) => b.Value.CompareTo(a.Value));
var sb = new System.Text.StringBuilder("frames=" + frames + "\n");
for (int i = 0; i < System.Math.Min(25, list.Count); i++)
    sb.AppendLine(list[i].Value.ToString("0") + " B in " + counts[list[i].Key] + " frames :: " + list[i].Key);
return sb.ToString();
