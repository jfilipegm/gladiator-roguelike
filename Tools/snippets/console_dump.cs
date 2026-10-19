// Dumps the Unity console (errors + warnings, optionally logs) via the internal LogEntries API.
// Run with: Tools/ueval -f Tools/snippets/console_dump.cs
var t = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
var entryType = System.Type.GetType("UnityEditor.LogEntry,UnityEditor");
int count = (int)t.GetMethod("GetCount").Invoke(null, null);
var e = System.Activator.CreateInstance(entryType);
var fMsg = entryType.GetField("message"); var fMode = entryType.GetField("mode");
const int ErrMask = 1 | 2 | 16 | 64 | 256 | 2048 | 131072 | 1048576 | 2097152; // bit 23 = IsCalledFromManaged, bit 14 = MayIgnoreLineNumber (not severities)
const int WarnMask = 128 | 512 | 4096;
var sb = new System.Text.StringBuilder();
int errs = 0, warns = 0, logs = 0, bridge = 0;
t.GetMethod("StartGettingEntries").Invoke(null, null);
try {
  for (int i = 0; i < count; i++) {
    t.GetMethod("GetEntryInternal").Invoke(null, new object[] { i, e });
    int mode = (int)fMode.GetValue(e);
    string msg = (string)fMsg.GetValue(e);
    string kind = (mode & ErrMask) != 0 ? "ERROR" : (mode & WarnMask) != 0 ? "WARN" : "LOG";
    // the bridge's own request timeouts/aborts (builds, domain reloads) are noise, not project errors
    if (kind == "ERROR" && msg.StartsWith("Failed to handle /api/exec request")) { bridge++; continue; }
    if (kind == "ERROR") errs++; else if (kind == "WARN") warns++; else logs++;
    var lines = msg.Split('\n');
    string head = lines[0];
    if (kind != "LOG" && lines.Length > 1) head += "  ||  " + string.Join(" | ", System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(lines, 1), 3));
    sb.AppendLine(kind + ": " + head);
  }
} finally { t.GetMethod("EndGettingEntries").Invoke(null, null); }
return "errors=" + errs + " warnings=" + warns + " logs=" + logs + (bridge > 0 ? " (bridge noise " + bridge + ")" : "") + "\n" + sb.ToString();
