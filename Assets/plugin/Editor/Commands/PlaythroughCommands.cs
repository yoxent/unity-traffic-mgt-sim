using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcpPro
{
    public class PlaythroughCommands : BaseCommand
    {
        // Static storage for last playthrough results
        private static Dictionary<string, object> _lastResults;
        private static List<Dictionary<string, object>> _capturedLogs;
        private static List<float> _frameTimes;
        private static List<string> _screenshotPaths;
        private static bool _isRunning;
        private static float _startTime;
        private static float _endTime;
        private static List<object> _scheduledActions;
        private static int _nextActionIndex;
        private static bool _captureLogs;
        private static bool _captureFps;

        // Continuous capture state
        private static bool _isContinuousCapture;
        private static float _captureInterval;
        private static bool _trackMemory;
        private static float _nextCaptureTime;
        private static List<Dictionary<string, object>> _captureSnapshots;
        private static List<Dictionary<string, object>> _captureLogs2;
        private static List<float> _captureFrameTimes;

        public static void Register(CommandRouter router)
        {
            router.Register("run_playthrough", RunPlaythrough);
            router.Register("create_playthrough_scenario", CreatePlaythroughScenario);
            router.Register("get_playthrough_results", GetPlaythroughResults);
            router.Register("capture_play_session", CapturePlaySession);
        }

        private static object RunPlaythrough(Dictionary<string, object> p)
        {
            float duration = GetFloatParam(p, "duration", 10f);
            _captureLogs = GetBoolParam(p, "capture_logs", true);
            _captureFps = GetBoolParam(p, "capture_fps", true);

            // Parse actions
            _scheduledActions = new List<object>();
            if (p.ContainsKey("actions") && p["actions"] is List<object> actionList)
            {
                _scheduledActions = actionList;
            }
            _nextActionIndex = 0;

            // Initialize collection
            _capturedLogs = new List<Dictionary<string, object>>();
            _frameTimes = new List<float>();
            _screenshotPaths = new List<string>();
            _lastResults = null;
            _isRunning = true;

            // Register log callback
            if (_captureLogs)
            {
                Application.logMessageReceived += OnLogMessage;
            }

            // Enter play mode
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }

            _startTime = (float)EditorApplication.timeSinceStartup;
            _endTime = _startTime + duration;

            // Register update callback
            EditorApplication.update += PlaythroughUpdate;

            return new Dictionary<string, object>
            {
                { "status", "started" },
                { "duration", duration },
                { "actionCount", _scheduledActions.Count },
                { "captureLogs", _captureLogs },
                { "captureFps", _captureFps },
                { "message", $"Playthrough started for {duration}s with {_scheduledActions.Count} scheduled actions" }
            };
        }

        private static void PlaythroughUpdate()
        {
            if (!_isRunning) return;

            float now = (float)EditorApplication.timeSinceStartup;
            float elapsed = now - _startTime;

            // Track frame time
            if (_captureFps && EditorApplication.isPlaying)
            {
                _frameTimes.Add(Time.unscaledDeltaTime);
            }

            // Execute scheduled actions
            while (_nextActionIndex < _scheduledActions.Count)
            {
                var actionObj = _scheduledActions[_nextActionIndex] as Dictionary<string, object>;
                if (actionObj == null) { _nextActionIndex++; continue; }

                float actionTime = 0f;
                if (actionObj.ContainsKey("time"))
                {
                    if (actionObj["time"] is double d) actionTime = (float)d;
                    else if (actionObj["time"] is long l) actionTime = l;
                    else float.TryParse(actionObj["time"].ToString(), out actionTime);
                }

                if (elapsed < actionTime) break;

                string actionType = actionObj.ContainsKey("type") ? actionObj["type"].ToString() : "";
                string actionValue = actionObj.ContainsKey("value") ? actionObj["value"].ToString() : "";

                try
                {
                    switch (actionType)
                    {
                        case "key":
                            Debug.Log($"[Playthrough] Key press: {actionValue} at {elapsed:F2}s");
                            break;
                        case "screenshot":
                            string path = TakePlaythroughScreenshot(actionValue, elapsed);
                            _screenshotPaths.Add(path);
                            break;
                        case "mouse_click":
                            Debug.Log($"[Playthrough] Mouse click at {elapsed:F2}s");
                            break;
                        case "wait":
                            // No-op; time-based scheduling handles this
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Playthrough] Action error: {ex.Message}");
                }

                _nextActionIndex++;
            }

            // Check if playthrough is done
            if (now >= _endTime || !EditorApplication.isPlaying)
            {
                FinishPlaythrough();
            }
        }

        private static void FinishPlaythrough()
        {
            _isRunning = false;
            EditorApplication.update -= PlaythroughUpdate;

            if (_captureLogs)
            {
                Application.logMessageReceived -= OnLogMessage;
            }

            // Calculate FPS stats
            var fpsStats = new Dictionary<string, object>();
            if (_captureFps && _frameTimes.Count > 0)
            {
                var validTimes = _frameTimes.Where(t => t > 0).ToList();
                if (validTimes.Count > 0)
                {
                    var fpsValues = validTimes.Select(t => 1f / t).ToList();
                    fpsValues.Sort();

                    fpsStats["avgFps"] = Math.Round(fpsValues.Average(), 1);
                    fpsStats["minFps"] = Math.Round(fpsValues.Min(), 1);
                    fpsStats["maxFps"] = Math.Round(fpsValues.Max(), 1);
                    fpsStats["frameCount"] = validTimes.Count;

                    // 1% low
                    int onePercentIndex = Math.Max(0, (int)(validTimes.Count * 0.01f));
                    fpsStats["onePercentLowFps"] = Math.Round(fpsValues[onePercentIndex], 1);

                    // Average frame time
                    fpsStats["avgFrameTimeMs"] = Math.Round(validTimes.Average() * 1000, 2);

                    // 99th percentile frame time
                    validTimes.Sort();
                    int p99Index = Math.Min(validTimes.Count - 1, (int)(validTimes.Count * 0.99f));
                    fpsStats["p99FrameTimeMs"] = Math.Round(validTimes[p99Index] * 1000, 2);
                }
            }

            _lastResults = new Dictionary<string, object>
            {
                { "status", "completed" },
                { "totalDuration", Math.Round(EditorApplication.timeSinceStartup - _startTime, 2) },
                { "fps", fpsStats },
                { "logCount", _capturedLogs.Count },
                { "logs", _capturedLogs.Take(200).ToList() },
                { "screenshots", _screenshotPaths },
                { "actionsExecuted", _nextActionIndex }
            };

            // Exit play mode
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_isRunning) return;

            _capturedLogs.Add(new Dictionary<string, object>
            {
                { "time", Math.Round(EditorApplication.timeSinceStartup - _startTime, 3) },
                { "type", type.ToString() },
                { "message", condition },
                { "stackTrace", type == LogType.Error || type == LogType.Exception ? stackTrace : "" }
            });
        }

        private static string TakePlaythroughScreenshot(string name, float elapsed)
        {
            string filename = string.IsNullOrEmpty(name) ? $"playthrough_{elapsed:F1}s" : name;
            string dir = Path.Combine(Application.dataPath, "..", "PlaythroughCaptures");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{filename}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            return path;
        }

        private static object CreatePlaythroughScenario(Dictionary<string, object> p)
        {
            string name = GetStringParam(p, "name");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            var steps = p.ContainsKey("steps") ? p["steps"] as List<object> : null;
            if (steps == null || steps.Count == 0)
                throw new ArgumentException("steps is required and must be non-empty");

            string scriptPath = GetStringParam(p, "script_path");
            if (string.IsNullOrEmpty(scriptPath))
                scriptPath = $"Assets/Tests/Scenarios/{name}Scenario.cs";

            // Generate C# script
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {name}Scenario : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    private int _assertsPassed;");
            sb.AppendLine("    private int _assertsFailed;");
            sb.AppendLine();
            sb.AppendLine("    public IEnumerator RunScenario()");
            sb.AppendLine("    {");
            sb.AppendLine($"        Debug.Log(\"[Scenario] Starting: {name}\");");

            foreach (var stepObj in steps)
            {
                var step = stepObj as Dictionary<string, object>;
                if (step == null) continue;

                string action = step.ContainsKey("action") ? step["action"].ToString() : "";

                switch (action)
                {
                    case "wait":
                        float dur = 1f;
                        if (step.ContainsKey("duration"))
                        {
                            if (step["duration"] is double dv) dur = (float)dv;
                            else float.TryParse(step["duration"].ToString(), out dur);
                        }
                        sb.AppendLine($"        yield return new WaitForSeconds({dur}f);");
                        break;

                    case "press_key":
                        string key = step.ContainsKey("key") ? step["key"].ToString() : "Space";
                        sb.AppendLine($"        Debug.Log(\"[Scenario] Press key: {key}\");");
                        sb.AppendLine($"        // Simulate key press for {key}");
                        sb.AppendLine($"        yield return null;");
                        break;

                    case "assert_exists":
                        string obj = step.ContainsKey("object") ? step["object"].ToString() : "";
                        sb.AppendLine($"        if (GameObject.Find(\"{obj}\") != null)");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            Debug.Log(\"[Scenario] PASS: '{obj}' exists\");");
                        sb.AppendLine("            _assertsPassed++;");
                        sb.AppendLine("        }");
                        sb.AppendLine("        else");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            Debug.LogError(\"[Scenario] FAIL: '{obj}' not found\");");
                        sb.AppendLine("            _assertsFailed++;");
                        sb.AppendLine("        }");
                        break;

                    case "assert_not_exists":
                        string obj2 = step.ContainsKey("object") ? step["object"].ToString() : "";
                        sb.AppendLine($"        if (GameObject.Find(\"{obj2}\") == null)");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            Debug.Log(\"[Scenario] PASS: '{obj2}' does not exist\");");
                        sb.AppendLine("            _assertsPassed++;");
                        sb.AppendLine("        }");
                        sb.AppendLine("        else");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            Debug.LogError(\"[Scenario] FAIL: '{obj2}' still exists\");");
                        sb.AppendLine("            _assertsFailed++;");
                        sb.AppendLine("        }");
                        break;

                    case "screenshot":
                        string ssName = step.ContainsKey("name") ? step["name"].ToString() : "screenshot";
                        sb.AppendLine($"        ScreenCapture.CaptureScreenshot(\"PlaythroughCaptures/{ssName}.png\");");
                        sb.AppendLine($"        Debug.Log(\"[Scenario] Screenshot: {ssName}\");");
                        sb.AppendLine("        yield return null;");
                        break;

                    case "mouse_click":
                        sb.AppendLine("        // Mouse click simulation");
                        sb.AppendLine("        yield return null;");
                        break;

                    case "log":
                        string msg = step.ContainsKey("message") ? step["message"].ToString() : "";
                        sb.AppendLine($"        Debug.Log(\"[Scenario] {msg}\");");
                        break;
                }
            }

            sb.AppendLine($"        Debug.Log($\"[Scenario] Completed: {name} - Passed: {{_assertsPassed}}, Failed: {{_assertsFailed}}\");");
            sb.AppendLine("        yield break;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Write file
            string fullPath = Path.Combine(Application.dataPath, "..", scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "scriptPath", scriptPath },
                { "stepCount", steps.Count },
                { "message", $"Scenario '{name}' created at {scriptPath}" }
            };
        }

        private static object GetPlaythroughResults(Dictionary<string, object> p)
        {
            if (_lastResults == null)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "No playthrough results available. Run 'run_playthrough' first." }
                };
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "data", _lastResults }
            };
        }

        private static object CapturePlaySession(Dictionary<string, object> p)
        {
            string action = GetStringParam(p, "action");
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("action is required ('start' or 'stop')");

            if (action == "start")
            {
                if (_isContinuousCapture)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "Capture already in progress. Use 'stop' first." }
                    };
                }

                _captureInterval = GetFloatParam(p, "interval", 5f);
                _trackMemory = GetBoolParam(p, "track_memory", true);
                _captureSnapshots = new List<Dictionary<string, object>>();
                _captureLogs2 = new List<Dictionary<string, object>>();
                _captureFrameTimes = new List<float>();
                _isContinuousCapture = true;
                _startTime = (float)EditorApplication.timeSinceStartup;
                _nextCaptureTime = _startTime + _captureInterval;

                Application.logMessageReceived += OnCaptureLogMessage;
                EditorApplication.update += ContinuousCaptureUpdate;

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "status", "capturing" },
                    { "interval", _captureInterval },
                    { "trackMemory", _trackMemory },
                    { "message", $"Continuous capture started (screenshot every {_captureInterval}s)" }
                };
            }
            else if (action == "stop")
            {
                if (!_isContinuousCapture)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "No capture in progress." }
                    };
                }

                _isContinuousCapture = false;
                EditorApplication.update -= ContinuousCaptureUpdate;
                Application.logMessageReceived -= OnCaptureLogMessage;

                float totalTime = (float)(EditorApplication.timeSinceStartup - _startTime);

                // Calculate FPS stats
                var fpsStats = new Dictionary<string, object>();
                if (_captureFrameTimes.Count > 0)
                {
                    var valid = _captureFrameTimes.Where(t => t > 0).ToList();
                    if (valid.Count > 0)
                    {
                        var fps = valid.Select(t => 1f / t).ToList();
                        fpsStats["avgFps"] = Math.Round(fps.Average(), 1);
                        fpsStats["minFps"] = Math.Round(fps.Min(), 1);
                        fpsStats["maxFps"] = Math.Round(fps.Max(), 1);
                    }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "status", "stopped" },
                    { "totalDuration", Math.Round(totalTime, 2) },
                    { "fps", fpsStats },
                    { "snapshots", _captureSnapshots },
                    { "logCount", _captureLogs2.Count },
                    { "logs", _captureLogs2.Take(200).ToList() },
                    { "frameCount", _captureFrameTimes.Count }
                };
            }

            throw new ArgumentException("action must be 'start' or 'stop'");
        }

        private static void ContinuousCaptureUpdate()
        {
            if (!_isContinuousCapture) return;

            float now = (float)EditorApplication.timeSinceStartup;

            // Track frame times
            if (EditorApplication.isPlaying)
            {
                _captureFrameTimes.Add(Time.unscaledDeltaTime);
            }

            // Periodic snapshot
            if (now >= _nextCaptureTime)
            {
                float elapsed = now - _startTime;
                var snapshot = new Dictionary<string, object>
                {
                    { "time", Math.Round(elapsed, 2) }
                };

                if (_trackMemory)
                {
                    snapshot["totalMemoryMB"] = Math.Round(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0), 2);
                    snapshot["usedHeapMB"] = Math.Round(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0), 2);
                    snapshot["gcMemoryMB"] = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2);
                }

                if (_captureFrameTimes.Count > 0)
                {
                    var recent = _captureFrameTimes.Skip(Math.Max(0, _captureFrameTimes.Count - 60)).ToList();
                    var validRecent = recent.Where(t => t > 0).ToList();
                    if (validRecent.Count > 0)
                    {
                        snapshot["currentFps"] = Math.Round(validRecent.Select(t => 1f / t).Average(), 1);
                    }
                }

                // Take screenshot
                string dir = Path.Combine(Application.dataPath, "..", "PlaythroughCaptures");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string ssPath = Path.Combine(dir, $"capture_{elapsed:F0}s_{DateTime.Now:HHmmss}.png");
                ScreenCapture.CaptureScreenshot(ssPath);
                snapshot["screenshot"] = ssPath;

                _captureSnapshots.Add(snapshot);
                _nextCaptureTime = now + _captureInterval;
            }
        }

        private static void OnCaptureLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_isContinuousCapture) return;

            _captureLogs2.Add(new Dictionary<string, object>
            {
                { "time", Math.Round(EditorApplication.timeSinceStartup - _startTime, 3) },
                { "type", type.ToString() },
                { "message", condition }
            });
        }
    }
}
