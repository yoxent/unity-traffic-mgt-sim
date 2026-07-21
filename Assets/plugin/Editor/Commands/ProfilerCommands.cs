using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityMcpPro
{
    /// <summary>
    /// Profiler tools for performance analysis. Uses ProfilerDriver (UnityEditorInternal)
    /// and UnityEngine.Profiling APIs to gather and analyze runtime performance data.
    /// </summary>
    public class ProfilerCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("get_profiler_data", GetProfilerData);
            router.Register("get_profiler_frame", GetProfilerFrame);
            router.Register("start_profiler_capture", StartProfilerCapture);
            router.Register("get_profiler_summary", GetProfilerSummary);
        }

        // ───────────────────── helpers ─────────────────────

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }

        private static int GetLatestFrameIndex()
        {
            return ProfilerDriver.lastFrameIndex;
        }

        // ───────────────────── command handlers ─────────────────────

        private static object GetProfilerData(Dictionary<string, object> p)
        {
            bool profilerEnabled = ProfilerDriver.enabled;

            // Memory info from Profiler API
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long totalUnused = Profiler.GetTotalUnusedReservedMemoryLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long gfxMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long tempAllocator = Profiler.GetTempAllocatorSize();

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "profiler_enabled", profilerEnabled },
                { "memory", new Dictionary<string, object>
                    {
                        { "total_allocated", FormatBytes(totalAllocated) },
                        { "total_allocated_bytes", totalAllocated },
                        { "total_reserved", FormatBytes(totalReserved) },
                        { "total_reserved_bytes", totalReserved },
                        { "total_unused", FormatBytes(totalUnused) },
                        { "mono_heap", FormatBytes(monoHeap) },
                        { "mono_used", FormatBytes(monoUsed) },
                        { "gfx_driver_memory", FormatBytes(gfxMemory) },
                        { "temp_allocator", FormatBytes(tempAllocator) }
                    }
                }
            };

            // Frame data (only available when profiler has captured frames)
            if (profilerEnabled)
            {
                int lastFrame = GetLatestFrameIndex();
                int firstFrame = ProfilerDriver.firstFrameIndex;
                result["frame_range"] = new Dictionary<string, object>
                {
                    { "first", firstFrame },
                    { "last", lastFrame },
                    { "count", lastFrame - firstFrame + 1 }
                };

                // Try to get current frame timing
                if (lastFrame >= 0)
                {
                    try
                    {
                        using (var frameData = ProfilerDriver.GetRawFrameDataView(lastFrame, 0))
                        {
                            if (frameData != null && frameData.valid)
                            {
                                float cpuMs = frameData.frameTimeMs;
                                float fps = cpuMs > 0 ? 1000f / cpuMs : 0f;
                                result["current_frame"] = new Dictionary<string, object>
                                {
                                    { "frame_index", lastFrame },
                                    { "cpu_time_ms", Math.Round(cpuMs, 2) },
                                    { "fps", Math.Round(fps, 1) }
                                };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Frame data may not be accessible
                    }
                }
            }

            // Rendering stats from editor
            if (EditorApplication.isPlaying)
            {
                result["play_mode"] = true;
            }

            return result;
        }

        private static object GetProfilerFrame(Dictionary<string, object> p)
        {
            int frameIndex = GetIntParam(p, "frame_index", -1);
            string category = GetStringParam(p, "category", "CPU");

            if (!ProfilerDriver.enabled)
                throw new InvalidOperationException(
                    "Profiler is not enabled. Use start_profiler_capture to start recording first.");

            if (frameIndex < 0)
                frameIndex = GetLatestFrameIndex();

            if (frameIndex < ProfilerDriver.firstFrameIndex || frameIndex > ProfilerDriver.lastFrameIndex)
                throw new ArgumentException(
                    $"Frame {frameIndex} is out of range [{ProfilerDriver.firstFrameIndex}, {ProfilerDriver.lastFrameIndex}]");

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "frame_index", frameIndex },
                { "category", category }
            };

            switch (category.ToUpperInvariant())
            {
                case "CPU":
                    result["data"] = GetCpuFrameData(frameIndex);
                    break;
                case "GPU":
                    result["data"] = GetGpuFrameData(frameIndex);
                    break;
                case "MEMORY":
                    result["data"] = GetMemoryFrameData(frameIndex);
                    break;
                case "RENDERING":
                    result["data"] = GetRenderingFrameData(frameIndex);
                    break;
                case "AUDIO":
                    result["data"] = GetAudioFrameData(frameIndex);
                    break;
                default:
                    throw new ArgumentException($"Unknown category: {category}. Use: CPU, GPU, Memory, Rendering, Audio");
            }

            return result;
        }

        private static object GetCpuFrameData(int frameIndex)
        {
            var samples = new List<object>();

            using (var frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (frameData == null || !frameData.valid)
                    return new Dictionary<string, object> { { "error", "Frame data not available" } };

                float frameTimeMs = frameData.frameTimeMs;

                // Use HierarchyFrameDataView for structured data
                using (var hierarchy = ProfilerDriver.GetHierarchyFrameDataView(
                    frameIndex, 0, HierarchyFrameDataView.ViewModes.Default,
                    HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (hierarchy != null && hierarchy.valid)
                    {
                        int rootId = hierarchy.GetRootItemID();
                        CollectHierarchySamples(hierarchy, rootId, samples, 0, maxDepth: 3, maxItems: 30);
                    }
                }

                return new Dictionary<string, object>
                {
                    { "frame_time_ms", Math.Round(frameTimeMs, 2) },
                    { "fps", frameTimeMs > 0 ? Math.Round(1000.0 / frameTimeMs, 1) : 0 },
                    { "samples", samples }
                };
            }
        }

        private static void CollectHierarchySamples(
            HierarchyFrameDataView hierarchy, int itemId,
            List<object> samples, int depth, int maxDepth, int maxItems)
        {
            if (samples.Count >= maxItems || depth > maxDepth) return;

            string name = hierarchy.GetItemName(itemId);
            float totalPercent = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalPercent);
            float selfPercent = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnSelfPercent);
            float totalTimeMs = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalTime);
            int calls = (int)hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnCalls);
            float gcAlloc = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnGcMemory);

            if (depth > 0) // Skip root
            {
                samples.Add(new Dictionary<string, object>
                {
                    { "name", name },
                    { "depth", depth },
                    { "total_percent", Math.Round(totalPercent, 1) },
                    { "self_percent", Math.Round(selfPercent, 1) },
                    { "total_time_ms", Math.Round(totalTimeMs, 3) },
                    { "calls", calls },
                    { "gc_alloc_bytes", gcAlloc }
                });
            }

            // Recurse into children
            var children = new List<int>();
            hierarchy.GetItemChildren(itemId, children);
            foreach (int childId in children)
            {
                CollectHierarchySamples(hierarchy, childId, samples, depth + 1, maxDepth, maxItems);
            }
        }

        private static object GetGpuFrameData(int frameIndex)
        {
            // GPU data is on thread index 1 in some setups
            using (var frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (frameData == null || !frameData.valid)
                    return new Dictionary<string, object> { { "error", "GPU frame data not available" } };

                return new Dictionary<string, object>
                {
                    { "frame_time_ms", Math.Round(frameData.frameTimeMs, 2) },
                    { "note", "Detailed GPU profiling requires GPU profiler module enabled in Profiler window" }
                };
            }
        }

        private static object GetMemoryFrameData(int frameIndex)
        {
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long monoUsed = Profiler.GetMonoUsedSizeLong();

            return new Dictionary<string, object>
            {
                { "total_allocated", FormatBytes(totalAllocated) },
                { "total_reserved", FormatBytes(totalReserved) },
                { "mono_heap", FormatBytes(monoHeap) },
                { "mono_used", FormatBytes(monoUsed) },
                { "gfx_driver", FormatBytes(Profiler.GetAllocatedMemoryForGraphicsDriver()) }
            };
        }

        private static object GetRenderingFrameData(int frameIndex)
        {
            using (var frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (frameData == null || !frameData.valid)
                    return new Dictionary<string, object> { { "error", "Rendering frame data not available" } };

                var data = new Dictionary<string, object>
                {
                    { "frame_time_ms", Math.Round(frameData.frameTimeMs, 2) }
                };

                // Try to extract rendering counters
                try
                {
                    int batchesId = frameData.GetMarkerId("Batches Count");
                    int setPassId = frameData.GetMarkerId("SetPass Calls Count");
                    int trianglesId = frameData.GetMarkerId("Triangles Count");
                    int verticesId = frameData.GetMarkerId("Vertices Count");

                    if (batchesId != FrameDataView.invalidMarkerId)
                        data["batches"] = frameData.GetCounterValueAsInt(batchesId);
                    if (setPassId != FrameDataView.invalidMarkerId)
                        data["set_pass_calls"] = frameData.GetCounterValueAsInt(setPassId);
                    if (trianglesId != FrameDataView.invalidMarkerId)
                        data["triangles"] = frameData.GetCounterValueAsLong(trianglesId);
                    if (verticesId != FrameDataView.invalidMarkerId)
                        data["vertices"] = frameData.GetCounterValueAsLong(verticesId);
                }
                catch (Exception)
                {
                    data["note"] = "Some rendering counters may not be available in this Unity version";
                }

                return data;
            }
        }

        private static object GetAudioFrameData(int frameIndex)
        {
            var data = new Dictionary<string, object>();

            try
            {
                var audioConfig = AudioSettings.GetConfiguration();
                data["sample_rate"] = audioConfig.sampleRate;
                data["speaker_mode"] = audioConfig.speakerMode.ToString();
                data["dsp_buffer_size"] = audioConfig.dspBufferSize;
            }
            catch (Exception)
            {
                data["note"] = "Audio configuration not available";
            }

            return data;
        }

        private static object StartProfilerCapture(Dictionary<string, object> p)
        {
            string action = GetStringParam(p, "action");
            int duration = GetIntParam(p, "duration", 0);
            bool deepProfile = GetBoolParam(p, "deep_profile");

            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("action is required ('start' or 'stop')");

            switch (action.ToLowerInvariant())
            {
                case "start":
                {
                    if (deepProfile)
                    {
                        // Deep profiling must be set before enabling the profiler
                        ProfilerDriver.deepProfiling = true;
                    }

                    ProfilerDriver.enabled = true;
                    ProfilerDriver.ClearAllFrames();

                    var result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "action", "started" },
                        { "deep_profile", deepProfile }
                    };

                    if (duration > 0)
                    {
                        result["auto_stop_seconds"] = duration;
                        // Schedule auto-stop via EditorApplication.delayCall
                        float stopTime = (float)EditorApplication.timeSinceStartup + duration;
                        EditorApplication.CallbackFunction autoStop = null;
                        autoStop = () =>
                        {
                            if (EditorApplication.timeSinceStartup >= stopTime)
                            {
                                ProfilerDriver.enabled = false;
                                EditorApplication.update -= autoStop;
                                Debug.Log($"[MCP] Profiler auto-stopped after {duration} seconds. " +
                                          $"Captured frames: {ProfilerDriver.lastFrameIndex - ProfilerDriver.firstFrameIndex + 1}");
                            }
                        };
                        EditorApplication.update += autoStop;
                    }

                    return result;
                }

                case "stop":
                {
                    bool wasEnabled = ProfilerDriver.enabled;
                    ProfilerDriver.enabled = false;

                    int firstFrame = ProfilerDriver.firstFrameIndex;
                    int lastFrame = ProfilerDriver.lastFrameIndex;
                    int frameCount = lastFrame >= firstFrame ? lastFrame - firstFrame + 1 : 0;

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "action", "stopped" },
                        { "was_recording", wasEnabled },
                        { "frames_captured", frameCount },
                        { "frame_range", new Dictionary<string, object>
                            {
                                { "first", firstFrame },
                                { "last", lastFrame }
                            }
                        }
                    };
                }

                default:
                    throw new ArgumentException($"Unknown action: {action}. Use 'start' or 'stop'.");
            }
        }

        private static object GetProfilerSummary(Dictionary<string, object> p)
        {
            int frameCount = GetIntParam(p, "frame_count", 30);

            if (!ProfilerDriver.enabled && ProfilerDriver.lastFrameIndex < 0)
                throw new InvalidOperationException(
                    "No profiler data available. Use start_profiler_capture to record frames first.");

            int lastFrame = ProfilerDriver.lastFrameIndex;
            int firstFrame = Math.Max(ProfilerDriver.firstFrameIndex, lastFrame - frameCount + 1);
            int actualFrames = lastFrame - firstFrame + 1;

            if (actualFrames <= 0)
                throw new InvalidOperationException("No frames available for analysis");

            // Collect per-frame data
            var frameTimes = new List<float>();
            var topFunctions = new Dictionary<string, (double totalTime, int totalCalls, double gcAlloc)>();

            for (int f = firstFrame; f <= lastFrame; f++)
            {
                try
                {
                    using (var frameData = ProfilerDriver.GetRawFrameDataView(f, 0))
                    {
                        if (frameData != null && frameData.valid)
                        {
                            frameTimes.Add(frameData.frameTimeMs);
                        }
                    }

                    using (var hierarchy = ProfilerDriver.GetHierarchyFrameDataView(
                        f, 0, HierarchyFrameDataView.ViewModes.Default,
                        HierarchyFrameDataView.columnTotalTime, false))
                    {
                        if (hierarchy != null && hierarchy.valid)
                        {
                            int rootId = hierarchy.GetRootItemID();
                            CollectTopFunctions(hierarchy, rootId, topFunctions, 0, maxDepth: 2);
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip frames that can't be read
                }
            }

            if (frameTimes.Count == 0)
                throw new InvalidOperationException("Could not read any frame data");

            // Compute statistics
            float avgFrameTime = frameTimes.Average();
            float maxFrameTime = frameTimes.Max();
            float minFrameTime = frameTimes.Min();
            float avgFps = avgFrameTime > 0 ? 1000f / avgFrameTime : 0f;
            float minFps = maxFrameTime > 0 ? 1000f / maxFrameTime : 0f;
            float maxFps = minFrameTime > 0 ? 1000f / minFrameTime : 0f;

            // Percentile calculations
            var sorted = frameTimes.OrderBy(x => x).ToList();
            float p50 = sorted[(int)(sorted.Count * 0.5f)];
            float p95 = sorted[(int)(sorted.Count * 0.95f)];
            float p99 = sorted[Math.Min((int)(sorted.Count * 0.99f), sorted.Count - 1)];

            // Count spikes (frames > 2x average)
            int spikeCount = frameTimes.Count(t => t > avgFrameTime * 2f);

            // Top functions by total time
            var topByTime = topFunctions
                .OrderByDescending(kvp => kvp.Value.totalTime)
                .Take(10)
                .Select(kvp => new Dictionary<string, object>
                {
                    { "name", kvp.Key },
                    { "avg_time_ms", Math.Round(kvp.Value.totalTime / actualFrames, 3) },
                    { "total_time_ms", Math.Round(kvp.Value.totalTime, 2) },
                    { "total_calls", kvp.Value.totalCalls },
                    { "gc_alloc_bytes", Math.Round(kvp.Value.gcAlloc, 0) }
                })
                .ToList();

            // Top GC allocators
            var topByGC = topFunctions
                .Where(kvp => kvp.Value.gcAlloc > 0)
                .OrderByDescending(kvp => kvp.Value.gcAlloc)
                .Take(5)
                .Select(kvp => new Dictionary<string, object>
                {
                    { "name", kvp.Key },
                    { "gc_alloc_bytes", Math.Round(kvp.Value.gcAlloc, 0) },
                    { "total_calls", kvp.Value.totalCalls }
                })
                .ToList();

            // Identify bottlenecks
            var bottlenecks = new List<object>();

            if (avgFps < 30)
                bottlenecks.Add(new Dictionary<string, object>
                {
                    { "severity", "high" },
                    { "issue", $"Low average FPS: {avgFps:F1} (target: 30+)" },
                    { "suggestion", "Review top CPU consumers and optimize hot paths" }
                });
            else if (avgFps < 60)
                bottlenecks.Add(new Dictionary<string, object>
                {
                    { "severity", "medium" },
                    { "issue", $"Below 60 FPS target: {avgFps:F1}" },
                    { "suggestion", "Consider optimizing render pipeline or reducing scene complexity" }
                });

            if (spikeCount > actualFrames * 0.1f)
                bottlenecks.Add(new Dictionary<string, object>
                {
                    { "severity", "high" },
                    { "issue", $"Frequent frame spikes: {spikeCount}/{actualFrames} frames ({(float)spikeCount / actualFrames * 100:F0}%)" },
                    { "suggestion", "Check for GC allocations, asset loading, or physics spikes" }
                });

            if (topByGC.Count > 0)
            {
                var worstGC = topByGC[0];
                float gcBytes = Convert.ToSingle(worstGC["gc_alloc_bytes"]);
                if (gcBytes > 1024 * 10) // > 10KB per analysis period
                {
                    bottlenecks.Add(new Dictionary<string, object>
                    {
                        { "severity", gcBytes > 1024 * 100 ? "high" : "medium" },
                        { "issue", $"GC allocations detected in '{worstGC["name"]}': {FormatBytes((long)gcBytes)}" },
                        { "suggestion", "Reduce allocations by using object pooling, caching, or struct types" }
                    });
                }
            }

            float frameTimeVariance = frameTimes.Select(t => (t - avgFrameTime) * (t - avgFrameTime)).Average();
            float stdDev = Mathf.Sqrt(frameTimeVariance);
            if (stdDev > avgFrameTime * 0.5f)
            {
                bottlenecks.Add(new Dictionary<string, object>
                {
                    { "severity", "medium" },
                    { "issue", $"High frame time variance (stddev: {stdDev:F2}ms, avg: {avgFrameTime:F2}ms)" },
                    { "suggestion", "Investigate intermittent heavy operations (GC, IO, physics)" }
                });
            }

            // Memory snapshot
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long monoUsed = Profiler.GetMonoUsedSizeLong();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "frames_analyzed", actualFrames },
                { "frame_range", new Dictionary<string, object> { { "first", firstFrame }, { "last", lastFrame } } },
                { "timing", new Dictionary<string, object>
                    {
                        { "avg_frame_ms", Math.Round(avgFrameTime, 2) },
                        { "min_frame_ms", Math.Round(minFrameTime, 2) },
                        { "max_frame_ms", Math.Round(maxFrameTime, 2) },
                        { "avg_fps", Math.Round(avgFps, 1) },
                        { "min_fps", Math.Round(minFps, 1) },
                        { "max_fps", Math.Round(maxFps, 1) },
                        { "p50_ms", Math.Round(p50, 2) },
                        { "p95_ms", Math.Round(p95, 2) },
                        { "p99_ms", Math.Round(p99, 2) },
                        { "stddev_ms", Math.Round(stdDev, 2) },
                        { "spike_count", spikeCount }
                    }
                },
                { "top_cpu_functions", topByTime },
                { "top_gc_allocators", topByGC },
                { "memory_snapshot", new Dictionary<string, object>
                    {
                        { "total_allocated", FormatBytes(totalAllocated) },
                        { "mono_used", FormatBytes(monoUsed) }
                    }
                },
                { "bottlenecks", bottlenecks }
            };
        }

        private static void CollectTopFunctions(
            HierarchyFrameDataView hierarchy, int itemId,
            Dictionary<string, (double totalTime, int totalCalls, double gcAlloc)> functions,
            int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            if (depth > 0) // Skip root
            {
                string name = hierarchy.GetItemName(itemId);
                float totalTimeMs = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalTime);
                int calls = (int)hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnCalls);
                float gcAlloc = hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnGcMemory);

                if (functions.TryGetValue(name, out var existing))
                {
                    functions[name] = (existing.totalTime + totalTimeMs, existing.totalCalls + calls, existing.gcAlloc + gcAlloc);
                }
                else
                {
                    functions[name] = (totalTimeMs, calls, gcAlloc);
                }
            }

            var children = new List<int>();
            hierarchy.GetItemChildren(itemId, children);
            foreach (int childId in children)
            {
                CollectTopFunctions(hierarchy, childId, functions, depth + 1, maxDepth);
            }
        }
    }
}
