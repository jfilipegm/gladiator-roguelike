// Starts Unity Profiler recording (player loop only) so gc_profile_report.cs can attribute GC.Alloc samples.
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
UnityEditorInternal.ProfilerDriver.profileEditor = false;
UnityEditorInternal.ProfilerDriver.enabled = true;
UnityEngine.Profiling.Profiler.enabled = true;
return "profiler recording";
