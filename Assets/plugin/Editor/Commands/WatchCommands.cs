using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class WatchCommands : BaseCommand
    {
        private class PropertyWatch
        {
            public string Label;
            public string TargetPath;
            public string ComponentName;
            public string PropertyName;
            public float Interval;
            public float? ThresholdMin;
            public float? ThresholdMax;
            public float NextCheckTime;
            public List<WatchSample> History = new List<WatchSample>();
            public List<string> Violations = new List<string>();
            public string CurrentValue;
            public double NumericMin = double.MaxValue;
            public double NumericMax = double.MinValue;
            public double NumericSum;
            public int NumericCount;
        }

        private struct WatchSample
        {
            public float Time;
            public string Value;
        }

        private static readonly List<PropertyWatch> _watches = new List<PropertyWatch>();
        private static bool _updateRegistered;

        public static void Register(CommandRouter router)
        {
            router.Register("watch_property", WatchProperty);
            router.Register("unwatch_property", UnwatchProperty);
            router.Register("get_watch_values", GetWatchValues);
        }

        private static object WatchProperty(Dictionary<string, object> p)
        {
            string target = GetStringParam(p, "target");
            string component = GetStringParam(p, "component");
            string property = GetStringParam(p, "property");
            float interval = GetFloatParam(p, "interval", 0.5f);
            string label = GetStringParam(p, "label");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(component))
                throw new ArgumentException("component is required");
            if (string.IsNullOrEmpty(property))
                throw new ArgumentException("property is required");

            if (string.IsNullOrEmpty(label))
                label = $"{target}.{component}.{property}";

            // Validate the target exists
            var go = FindGameObject(target);
            var comp = FindComponent(go, component);

            // Verify property exists
            var propInfo = comp.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            var fieldInfo = propInfo == null ? comp.GetType().GetField(property, BindingFlags.Public | BindingFlags.Instance) : null;
            if (propInfo == null && fieldInfo == null)
                throw new ArgumentException($"Property '{property}' not found on {component}");

            // Remove existing watch with same label
            _watches.RemoveAll(w => w.Label == label);

            var watch = new PropertyWatch
            {
                Label = label,
                TargetPath = target,
                ComponentName = component,
                PropertyName = property,
                Interval = interval,
                NextCheckTime = (float)EditorApplication.timeSinceStartup
            };

            if (p.ContainsKey("threshold_min"))
                watch.ThresholdMin = GetFloatParam(p, "threshold_min");
            if (p.ContainsKey("threshold_max"))
                watch.ThresholdMax = GetFloatParam(p, "threshold_max");

            _watches.Add(watch);

            // Ensure update callback is registered
            if (!_updateRegistered)
            {
                EditorApplication.update += WatchUpdate;
                _updateRegistered = true;
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "label", label },
                { "target", target },
                { "component", component },
                { "property", property },
                { "interval", interval },
                { "thresholdMin", watch.ThresholdMin.HasValue ? (object)watch.ThresholdMin.Value : null },
                { "thresholdMax", watch.ThresholdMax.HasValue ? (object)watch.ThresholdMax.Value : null },
                { "totalWatches", _watches.Count },
                { "message", $"Watching '{label}' every {interval}s" }
            };
        }

        private static object UnwatchProperty(Dictionary<string, object> p)
        {
            string label = GetStringParam(p, "label");

            int removed;
            if (string.IsNullOrEmpty(label))
            {
                removed = _watches.Count;
                _watches.Clear();
            }
            else
            {
                removed = _watches.RemoveAll(w => w.Label == label);
            }

            // Unregister update if no watches left
            if (_watches.Count == 0 && _updateRegistered)
            {
                EditorApplication.update -= WatchUpdate;
                _updateRegistered = false;
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "removed", removed },
                { "remainingWatches", _watches.Count },
                { "message", string.IsNullOrEmpty(label)
                    ? $"Cleared all watches ({removed} removed)"
                    : $"Removed watch '{label}'" }
            };
        }

        private static object GetWatchValues(Dictionary<string, object> p)
        {
            var watchData = new List<object>();

            foreach (var w in _watches)
            {
                var entry = new Dictionary<string, object>
                {
                    { "label", w.Label },
                    { "target", w.TargetPath },
                    { "component", w.ComponentName },
                    { "property", w.PropertyName },
                    { "currentValue", w.CurrentValue ?? "N/A" },
                    { "sampleCount", w.History.Count }
                };

                if (w.NumericCount > 0)
                {
                    entry["min"] = Math.Round(w.NumericMin, 4);
                    entry["max"] = Math.Round(w.NumericMax, 4);
                    entry["avg"] = Math.Round(w.NumericSum / w.NumericCount, 4);
                }

                if (w.Violations.Count > 0)
                {
                    entry["violationCount"] = w.Violations.Count;
                    entry["recentViolations"] = w.Violations.Skip(Math.Max(0, w.Violations.Count - 10)).ToList();
                }

                // Last 20 samples
                var recentSamples = w.History.Skip(Math.Max(0, w.History.Count - 20))
                    .Select(s => new Dictionary<string, object>
                    {
                        { "time", Math.Round(s.Time, 3) },
                        { "value", s.Value }
                    }).ToList();
                entry["recentHistory"] = recentSamples;

                watchData.Add(entry);
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "watchCount", _watches.Count },
                { "watches", watchData }
            };
        }

        private static void WatchUpdate()
        {
            if (_watches.Count == 0) return;

            float now = (float)EditorApplication.timeSinceStartup;

            foreach (var w in _watches)
            {
                if (now < w.NextCheckTime) continue;
                w.NextCheckTime = now + w.Interval;

                try
                {
                    var go = GameObject.Find(w.TargetPath);
                    if (go == null)
                    {
                        // Try recursive find
                        try { go = FindGameObject(w.TargetPath); } catch { }
                    }
                    if (go == null)
                    {
                        w.CurrentValue = "[GameObject not found]";
                        continue;
                    }

                    Component comp = null;
                    foreach (var c in go.GetComponents<Component>())
                    {
                        if (c != null && c.GetType().Name.Equals(w.ComponentName, StringComparison.OrdinalIgnoreCase))
                        {
                            comp = c;
                            break;
                        }
                    }

                    if (comp == null)
                    {
                        w.CurrentValue = "[Component not found]";
                        continue;
                    }

                    object val = null;
                    var propInfo = comp.GetType().GetProperty(w.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (propInfo != null)
                        val = propInfo.GetValue(comp);
                    else
                    {
                        var fieldInfo = comp.GetType().GetField(w.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                        if (fieldInfo != null)
                            val = fieldInfo.GetValue(comp);
                    }

                    string valStr = val?.ToString() ?? "null";
                    w.CurrentValue = valStr;

                    w.History.Add(new WatchSample { Time = now, Value = valStr });

                    // Cap history to 1000 samples
                    if (w.History.Count > 1000)
                        w.History.RemoveRange(0, w.History.Count - 1000);

                    // Track numeric stats
                    if (double.TryParse(valStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                    {
                        if (numVal < w.NumericMin) w.NumericMin = numVal;
                        if (numVal > w.NumericMax) w.NumericMax = numVal;
                        w.NumericSum += numVal;
                        w.NumericCount++;

                        // Threshold checks
                        if (w.ThresholdMin.HasValue && numVal < w.ThresholdMin.Value)
                        {
                            string violation = $"[{now:F1}s] {numVal} < min threshold {w.ThresholdMin.Value}";
                            w.Violations.Add(violation);
                            Debug.LogWarning($"[Watch] {w.Label}: {violation}");
                        }
                        if (w.ThresholdMax.HasValue && numVal > w.ThresholdMax.Value)
                        {
                            string violation = $"[{now:F1}s] {numVal} > max threshold {w.ThresholdMax.Value}";
                            w.Violations.Add(violation);
                            Debug.LogWarning($"[Watch] {w.Label}: {violation}");
                        }

                        // Cap violations list
                        if (w.Violations.Count > 500)
                            w.Violations.RemoveRange(0, w.Violations.Count - 500);
                    }
                }
                catch (Exception ex)
                {
                    w.CurrentValue = $"[Error: {ex.Message}]";
                }
            }
        }
    }
}
