using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class BenchmarkCommands : BaseCommand
    {
        private static bool _isRunning;
        private static float _startTime;
        private static float _warmUpEnd;
        private static float _endTime;
        private static string _benchmarkName;
        private static List<float> _frameTimes;
        private static List<double> _memorySnapshots;
        private static bool _warmUpDone;

        private const string BenchmarkPrefsPrefix = "UnityMcpPro_Benchmark_";
        private const string BenchmarkListKey = "UnityMcpPro_BenchmarkList";

        public static void Register(CommandRouter router)
        {
            router.Register("run_benchmark", RunBenchmark);
            router.Register("compare_benchmarks", CompareBenchmarks);
            router.Register("get_benchmark_history", GetBenchmarkHistory);
            router.Register("create_benchmark_profile", CreateBenchmarkProfile);
        }

        private static object RunBenchmark(Dictionary<string, object> p)
        {
            float duration = GetFloatParam(p, "duration", 10f);
            float warmUp = GetFloatParam(p, "warm_up", 2f);
            _benchmarkName = GetStringParam(p, "name", $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}");

            _frameTimes = new List<float>();
            _memorySnapshots = new List<double>();
            _warmUpDone = false;
            _isRunning = true;

            // Enter play mode if not already
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }

            _startTime = (float)EditorApplication.timeSinceStartup;
            _warmUpEnd = _startTime + warmUp;
            _endTime = _warmUpEnd + duration;

            EditorApplication.update += BenchmarkUpdate;

            return new Dictionary<string, object>
            {
                { "status", "started" },
                { "name", _benchmarkName },
                { "warmUp", warmUp },
                { "duration", duration },
                { "message", $"Benchmark '{_benchmarkName}' started ({warmUp}s warm-up + {duration}s measurement)" }
            };
        }

        private static void BenchmarkUpdate()
        {
            if (!_isRunning) return;

            float now = (float)EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                // Play mode ended unexpectedly
                FinishBenchmark(true);
                return;
            }

            // Warm-up phase
            if (!_warmUpDone && now >= _warmUpEnd)
            {
                _warmUpDone = true;
            }

            // Measurement phase
            if (_warmUpDone)
            {
                _frameTimes.Add(Time.unscaledDeltaTime);

                // Memory snapshot every ~60 frames
                if (_frameTimes.Count % 60 == 0)
                {
                    _memorySnapshots.Add(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0));
                }
            }

            // Done
            if (now >= _endTime)
            {
                FinishBenchmark(false);
            }
        }

        private static void FinishBenchmark(bool aborted)
        {
            _isRunning = false;
            EditorApplication.update -= BenchmarkUpdate;

            var results = new Dictionary<string, object>
            {
                { "name", _benchmarkName },
                { "date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "aborted", aborted }
            };

            if (_frameTimes.Count > 0)
            {
                var validTimes = _frameTimes.Where(t => t > 0).ToList();
                if (validTimes.Count > 0)
                {
                    var fpsValues = validTimes.Select(t => 1f / t).OrderBy(f => f).ToList();

                    double avgFps = Math.Round(fpsValues.Average(), 2);
                    double minFps = Math.Round(fpsValues.Min(), 2);
                    double maxFps = Math.Round(fpsValues.Max(), 2);

                    // 1% low FPS
                    int onePercentCount = Math.Max(1, (int)(fpsValues.Count * 0.01f));
                    double onePercentLow = Math.Round(fpsValues.Take(onePercentCount).Average(), 2);

                    // Frame time percentiles
                    var sortedTimes = validTimes.OrderBy(t => t).ToList();
                    int p99Index = Math.Min(sortedTimes.Count - 1, (int)(sortedTimes.Count * 0.99f));
                    double p99FrameTime = Math.Round(sortedTimes[p99Index] * 1000, 2);
                    double avgFrameTime = Math.Round(validTimes.Average() * 1000, 2);

                    results["avgFps"] = avgFps;
                    results["minFps"] = minFps;
                    results["maxFps"] = maxFps;
                    results["onePercentLowFps"] = onePercentLow;
                    results["avgFrameTimeMs"] = avgFrameTime;
                    results["p99FrameTimeMs"] = p99FrameTime;
                    results["frameCount"] = validTimes.Count;
                }
            }

            if (_memorySnapshots.Count > 0)
            {
                results["avgMemoryMB"] = Math.Round(_memorySnapshots.Average(), 2);
                results["peakMemoryMB"] = Math.Round(_memorySnapshots.Max(), 2);
                results["minMemoryMB"] = Math.Round(_memorySnapshots.Min(), 2);
            }

            // Save to EditorPrefs
            string json = JsonUtility.ToJson(new SerializableBenchmark(results));
            EditorPrefs.SetString(BenchmarkPrefsPrefix + _benchmarkName, json);

            // Update benchmark list
            var list = GetBenchmarkList();
            if (!list.Contains(_benchmarkName))
            {
                list.Add(_benchmarkName);
                EditorPrefs.SetString(BenchmarkListKey, string.Join("|", list));
            }

            // Exit play mode
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static object CompareBenchmarks(Dictionary<string, object> p)
        {
            string nameA = GetStringParam(p, "name_a");
            string nameB = GetStringParam(p, "name_b");

            if (string.IsNullOrEmpty(nameA) || string.IsNullOrEmpty(nameB))
                throw new ArgumentException("Both name_a and name_b are required");

            var dataA = LoadBenchmark(nameA);
            var dataB = LoadBenchmark(nameB);

            if (dataA == null) throw new ArgumentException($"Benchmark '{nameA}' not found");
            if (dataB == null) throw new ArgumentException($"Benchmark '{nameB}' not found");

            var comparison = new Dictionary<string, object>
            {
                { "nameA", nameA },
                { "nameB", nameB },
                { "metrics", new Dictionary<string, object>() }
            };

            var metrics = comparison["metrics"] as Dictionary<string, object>;
            string[] compareKeys = { "avgFps", "minFps", "maxFps", "onePercentLowFps", "avgFrameTimeMs", "p99FrameTimeMs", "avgMemoryMB", "peakMemoryMB" };

            foreach (var key in compareKeys)
            {
                if (dataA.ContainsKey(key) && dataB.ContainsKey(key))
                {
                    double valA = Convert.ToDouble(dataA[key]);
                    double valB = Convert.ToDouble(dataB[key]);
                    double delta = Math.Round(valB - valA, 2);
                    double percent = valA != 0 ? Math.Round((valB - valA) / valA * 100, 1) : 0;

                    metrics[key] = new Dictionary<string, object>
                    {
                        { "valueA", valA },
                        { "valueB", valB },
                        { "delta", delta },
                        { "percentChange", percent }
                    };
                }
            }

            return comparison;
        }

        private static object GetBenchmarkHistory(Dictionary<string, object> p)
        {
            var list = GetBenchmarkList();
            var benchmarks = new List<object>();

            foreach (var name in list)
            {
                var data = LoadBenchmark(name);
                if (data != null)
                {
                    var summary = new Dictionary<string, object>
                    {
                        { "name", name }
                    };

                    string[] summaryKeys = { "date", "avgFps", "minFps", "p99FrameTimeMs", "avgMemoryMB", "peakMemoryMB", "frameCount", "aborted" };
                    foreach (var key in summaryKeys)
                    {
                        if (data.ContainsKey(key))
                            summary[key] = data[key];
                    }
                    benchmarks.Add(summary);
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "count", benchmarks.Count },
                { "benchmarks", benchmarks }
            };
        }

        private static object CreateBenchmarkProfile(Dictionary<string, object> p)
        {
            string name = GetStringParam(p, "name");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            var profile = new Dictionary<string, object>
            {
                { "name", name },
                { "createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            var cameraPositions = GetStringListParam(p, "camera_positions");
            if (cameraPositions != null)
                profile["cameraPositions"] = cameraPositions;

            if (p.ContainsKey("quality_levels") && p["quality_levels"] is List<object> ql)
            {
                profile["qualityLevels"] = ql.Select(x =>
                {
                    if (x is double d) return (int)d;
                    if (x is long l) return (int)l;
                    int.TryParse(x.ToString(), out int r);
                    return r;
                }).ToList();
            }

            var resolutions = GetStringListParam(p, "resolutions");
            if (resolutions != null)
                profile["resolutions"] = resolutions;

            // Save profile
            string profileKey = BenchmarkPrefsPrefix + "Profile_" + name;
            EditorPrefs.SetString(profileKey, JsonUtility.ToJson(new SerializableBenchmark(profile)));

            return new Dictionary<string, object>
            {
                { "success", true },
                { "profile", profile },
                { "message", $"Benchmark profile '{name}' created" }
            };
        }

        // Helper: load benchmark data from EditorPrefs
        private static Dictionary<string, object> LoadBenchmark(string name)
        {
            string key = BenchmarkPrefsPrefix + name;
            if (!EditorPrefs.HasKey(key)) return null;

            string json = EditorPrefs.GetString(key);
            try
            {
                var sb = JsonUtility.FromJson<SerializableBenchmark>(json);
                return sb?.ToDictionary();
            }
            catch
            {
                return null;
            }
        }

        private static List<string> GetBenchmarkList()
        {
            string raw = EditorPrefs.GetString(BenchmarkListKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        [Serializable]
        private class SerializableBenchmark
        {
            public string jsonData;

            public SerializableBenchmark() { }

            public SerializableBenchmark(Dictionary<string, object> data)
            {
                // Simple key=value serialization for EditorPrefs storage
                var parts = new List<string>();
                foreach (var kvp in data)
                {
                    if (kvp.Value is IEnumerable<object>) continue; // skip complex types
                    parts.Add($"{kvp.Key}={kvp.Value}");
                }
                jsonData = string.Join("\n", parts);
            }

            public Dictionary<string, object> ToDictionary()
            {
                var result = new Dictionary<string, object>();
                if (string.IsNullOrEmpty(jsonData)) return result;

                foreach (var line in jsonData.Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq);
                    string v = line.Substring(eq + 1);

                    if (double.TryParse(v, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double dv))
                        result[k] = dv;
                    else if (bool.TryParse(v, out bool bv))
                        result[k] = bv;
                    else
                        result[k] = v;
                }
                return result;
            }
        }
    }
}
