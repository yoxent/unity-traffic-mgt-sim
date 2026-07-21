using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class AddressableCommands : BaseCommand
    {
        // All access via reflection since com.unity.addressables may not be installed
        private static Type _settingsType;
        private static Type _defaultObjectType;
        private static Type _entryType;
        private static Type _groupType;
        private static Type _groupSchemaType;
        private static Type _bundledSchemaType;
        private static bool _typesResolved;

        public static void Register(CommandRouter router)
        {
            router.Register("create_addressable_group", CreateAddressableGroup);
            router.Register("set_addressable_address", SetAddressableAddress);
            router.Register("build_addressables", BuildAddressables);
            router.Register("get_addressable_info", GetAddressableInfo);
            router.Register("analyze_addressables", AnalyzeAddressables);
        }

        private static bool ResolveTypes()
        {
            if (_typesResolved) return _settingsType != null;

            _typesResolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (_settingsType == null)
                        _settingsType = asm.GetType("UnityEditor.AddressableAssets.AddressableAssetSettings");
                    if (_defaultObjectType == null)
                        _defaultObjectType = asm.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                    if (_entryType == null)
                        _entryType = asm.GetType("UnityEditor.AddressableAssets.AddressableAssetEntry");
                    if (_groupType == null)
                        _groupType = asm.GetType("UnityEditor.AddressableAssets.AddressableAssetGroup");
                    if (_groupSchemaType == null)
                        _groupSchemaType = asm.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetGroupSchema");
                    if (_bundledSchemaType == null)
                        _bundledSchemaType = asm.GetType("UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema");
                }
                catch { }
            }

            return _settingsType != null;
        }

        private static object GetSettings()
        {
            if (!ResolveTypes())
                throw new InvalidOperationException("Addressables package is not installed. Install com.unity.addressables via Package Manager.");

            var settingsProp = _defaultObjectType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            var settings = settingsProp?.GetValue(null);

            if (settings == null)
                throw new InvalidOperationException("Addressable Asset Settings not initialized. Open Window > Asset Management > Addressables > Groups to initialize.");

            return settings;
        }

        private static object CreateAddressableGroup(Dictionary<string, object> p)
        {
            string name = GetStringParam(p, "name");
            string schema = GetStringParam(p, "schema", "PackedAssets");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            var settings = GetSettings();

            // CreateGroup(string name, bool setAsDefaultGroup, bool readOnly, bool postEvent, List<AddressableAssetGroupSchema> schemas, params Type[] types)
            var createGroupMethod = _settingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "CreateGroup" && m.GetParameters().Length >= 3);

            if (createGroupMethod == null)
                throw new InvalidOperationException("CreateGroup method not found on AddressableAssetSettings");

            // Determine schema types to add
            var schemaTypes = new List<Type>();
            if (schema == "PackedAssets" && _bundledSchemaType != null)
            {
                schemaTypes.Add(_bundledSchemaType);
            }

            object group;
            try
            {
                // Try the common overload: CreateGroup(string, bool, bool, bool, List<Schema>, Type[])
                var listType = typeof(List<>).MakeGenericType(_groupSchemaType);
                var schemaList = Activator.CreateInstance(listType);
                var typeArray = Array.CreateInstance(typeof(Type), schemaTypes.Count);
                for (int i = 0; i < schemaTypes.Count; i++)
                    typeArray.SetValue(schemaTypes[i], i);

                group = createGroupMethod.Invoke(settings, new object[] { name, false, false, true, schemaList, typeArray });
            }
            catch
            {
                // Fallback: simpler overload
                group = createGroupMethod.Invoke(settings, new object[] { name, false, false, true, null, new Type[0] });
            }

            if (group == null)
                throw new InvalidOperationException("Failed to create Addressable group");

            EditorUtility.SetDirty(settings as UnityEngine.Object);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "groupName", name },
                { "schema", schema },
                { "message", $"Addressable group '{name}' created with {schema} schema" }
            };
        }

        private static object SetAddressableAddress(Dictionary<string, object> p)
        {
            string path = GetStringParam(p, "path");
            string address = GetStringParam(p, "address");
            string groupName = GetStringParam(p, "group");
            string[] labels = GetStringListParam(p, "labels");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            if (string.IsNullOrEmpty(address))
                address = path;

            var settings = GetSettings();

            // Get GUID
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException($"Asset not found at path: {path}");

            // Find or use default group
            object targetGroup = null;
            if (!string.IsNullOrEmpty(groupName))
            {
                // Find group by name
                var groupsProp = _settingsType.GetProperty("groups", BindingFlags.Public | BindingFlags.Instance);
                var groups = groupsProp?.GetValue(settings) as System.Collections.IList;
                if (groups != null)
                {
                    foreach (var g in groups)
                    {
                        var gNameProp = _groupType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                        if (gNameProp?.GetValue(g)?.ToString() == groupName)
                        {
                            targetGroup = g;
                            break;
                        }
                    }
                }
                if (targetGroup == null)
                    throw new ArgumentException($"Addressable group '{groupName}' not found");
            }
            else
            {
                var defaultGroupProp = _settingsType.GetProperty("DefaultGroup", BindingFlags.Public | BindingFlags.Instance);
                targetGroup = defaultGroupProp?.GetValue(settings);
            }

            // CreateOrMoveEntry(string guid, AddressableAssetGroup group, bool readOnly = false, bool postEvent = true)
            var createEntryMethod = _settingsType.GetMethod("CreateOrMoveEntry",
                BindingFlags.Public | BindingFlags.Instance,
                null, new Type[] { typeof(string), _groupType, typeof(bool), typeof(bool) }, null);

            if (createEntryMethod == null)
            {
                // Try simpler overload
                createEntryMethod = _settingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "CreateOrMoveEntry");
            }

            object entry = createEntryMethod?.Invoke(settings, new object[] { guid, targetGroup, false, true });
            if (entry == null)
                throw new InvalidOperationException("Failed to create addressable entry");

            // Set address
            var addressProp = _entryType.GetProperty("address", BindingFlags.Public | BindingFlags.Instance);
            addressProp?.SetValue(entry, address);

            // Set labels
            if (labels != null && labels.Length > 0)
            {
                var setLabelMethod = _entryType.GetMethod("SetLabel", BindingFlags.Public | BindingFlags.Instance);
                // Also need to add labels to settings
                var addLabelMethod = _settingsType.GetMethod("AddLabel", BindingFlags.Public | BindingFlags.Instance);

                foreach (var label in labels)
                {
                    addLabelMethod?.Invoke(settings, new object[] { label, true });
                    setLabelMethod?.Invoke(entry, new object[] { label, true, true, true });
                }
            }

            EditorUtility.SetDirty(settings as UnityEngine.Object);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "address", address },
                { "group", groupName ?? "Default" },
                { "labels", labels ?? new string[0] },
                { "message", $"Asset '{path}' marked as Addressable with address '{address}'" }
            };
        }

        private static object BuildAddressables(Dictionary<string, object> p)
        {
            bool clean = GetBoolParam(p, "clean", false);

            if (!ResolveTypes())
                throw new InvalidOperationException("Addressables package is not installed.");

            try
            {
                if (clean)
                {
                    // Clean build
                    var cleanMethod = _settingsType.GetMethod("CleanPlayerContent",
                        BindingFlags.Public | BindingFlags.Static);
                    cleanMethod?.Invoke(null, null);
                }

                // BuildPlayerContent
                var buildMethod = _settingsType.GetMethod("BuildPlayerContent",
                    BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);

                if (buildMethod == null)
                {
                    // Try with parameters
                    buildMethod = _settingsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "BuildPlayerContent");
                }

                if (buildMethod == null)
                    throw new InvalidOperationException("BuildPlayerContent method not found");

                var paramCount = buildMethod.GetParameters().Length;
                object result;
                if (paramCount == 0)
                    result = buildMethod.Invoke(null, null);
                else
                    result = buildMethod.Invoke(null, new object[paramCount]);

                // Try to get build result info
                string resultStr = result?.ToString() ?? "Build completed";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "clean", clean },
                    { "result", resultStr },
                    { "message", $"Addressables build completed{(clean ? " (clean)" : "")}" }
                };
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException($"Addressables build failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private static object GetAddressableInfo(Dictionary<string, object> p)
        {
            var settings = GetSettings();

            var groupsProp = _settingsType.GetProperty("groups", BindingFlags.Public | BindingFlags.Instance);
            var groups = groupsProp?.GetValue(settings) as System.Collections.IList;

            var labelsProp = _settingsType.GetMethod("GetLabels", BindingFlags.Public | BindingFlags.Instance);
            var allLabels = labelsProp?.Invoke(settings, null) as System.Collections.IList;

            var groupList = new List<object>();
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null) continue;

                    var gNameProp = _groupType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    var entriesProp = _groupType.GetProperty("entries", BindingFlags.Public | BindingFlags.Instance);
                    var isDefaultProp = _groupType.GetProperty("IsDefaultGroup", BindingFlags.Public | BindingFlags.Instance);

                    var entries = entriesProp?.GetValue(group) as System.Collections.IList;

                    var entryList = new List<object>();
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (entry == null) continue;

                            var addrProp = _entryType.GetProperty("address", BindingFlags.Public | BindingFlags.Instance);
                            var guidProp = _entryType.GetProperty("guid", BindingFlags.Public | BindingFlags.Instance);
                            var assetPathProp = _entryType.GetProperty("AssetPath", BindingFlags.Public | BindingFlags.Instance);

                            var entryLabels = new List<string>();
                            var entryLabelsProp = _entryType.GetProperty("labels", BindingFlags.Public | BindingFlags.Instance);
                            var elabels = entryLabelsProp?.GetValue(entry) as System.Collections.IEnumerable;
                            if (elabels != null)
                            {
                                foreach (var l in elabels)
                                    entryLabels.Add(l.ToString());
                            }

                            entryList.Add(new Dictionary<string, object>
                            {
                                { "address", addrProp?.GetValue(entry)?.ToString() ?? "" },
                                { "guid", guidProp?.GetValue(entry)?.ToString() ?? "" },
                                { "assetPath", assetPathProp?.GetValue(entry)?.ToString() ?? "" },
                                { "labels", entryLabels }
                            });
                        }
                    }

                    groupList.Add(new Dictionary<string, object>
                    {
                        { "name", gNameProp?.GetValue(group)?.ToString() ?? "" },
                        { "isDefault", isDefaultProp?.GetValue(group) ?? false },
                        { "entryCount", entryList.Count },
                        { "entries", entryList }
                    });
                }
            }

            var labelList = new List<string>();
            if (allLabels != null)
            {
                foreach (var l in allLabels)
                    labelList.Add(l.ToString());
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "groupCount", groupList.Count },
                { "groups", groupList },
                { "labels", labelList }
            };
        }

        private static object AnalyzeAddressables(Dictionary<string, object> p)
        {
            var settings = GetSettings();
            var groupsProp = _settingsType.GetProperty("groups", BindingFlags.Public | BindingFlags.Instance);
            var groups = groupsProp?.GetValue(settings) as System.Collections.IList;

            var issues = new List<object>();
            var allEntries = new Dictionary<string, List<string>>(); // guid -> group names
            int totalEntries = 0;

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null) continue;

                    var gNameProp = _groupType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    string gName = gNameProp?.GetValue(group)?.ToString() ?? "Unknown";

                    var entriesProp = _groupType.GetProperty("entries", BindingFlags.Public | BindingFlags.Instance);
                    var entries = entriesProp?.GetValue(group) as System.Collections.IList;

                    if (entries == null) continue;

                    int groupSize = 0;
                    foreach (var entry in entries)
                    {
                        if (entry == null) continue;
                        totalEntries++;

                        var guidProp = _entryType.GetProperty("guid", BindingFlags.Public | BindingFlags.Instance);
                        var assetPathProp = _entryType.GetProperty("AssetPath", BindingFlags.Public | BindingFlags.Instance);
                        string guid = guidProp?.GetValue(entry)?.ToString() ?? "";
                        string assetPath = assetPathProp?.GetValue(entry)?.ToString() ?? "";

                        // Track for duplicate detection
                        if (!string.IsNullOrEmpty(guid))
                        {
                            if (!allEntries.ContainsKey(guid))
                                allEntries[guid] = new List<string>();
                            allEntries[guid].Add(gName);
                        }

                        // Check for missing asset
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            string fullGuid = AssetDatabase.AssetPathToGUID(assetPath);
                            if (string.IsNullOrEmpty(fullGuid))
                            {
                                issues.Add(new Dictionary<string, object>
                                {
                                    { "type", "missing_asset" },
                                    { "severity", "error" },
                                    { "group", gName },
                                    { "path", assetPath },
                                    { "message", $"Asset missing: {assetPath} in group '{gName}'" }
                                });
                            }
                            else
                            {
                                // Estimate size
                                var importer = AssetImporter.GetAtPath(assetPath);
                                if (importer != null)
                                {
                                    var fileInfo = new System.IO.FileInfo(System.IO.Path.Combine(Application.dataPath, "..", assetPath));
                                    if (fileInfo.Exists)
                                        groupSize += (int)fileInfo.Length;
                                }
                            }
                        }
                    }

                    // Large bundle warning
                    double groupSizeMB = groupSize / (1024.0 * 1024.0);
                    if (groupSizeMB > 50)
                    {
                        issues.Add(new Dictionary<string, object>
                        {
                            { "type", "large_bundle" },
                            { "severity", "warning" },
                            { "group", gName },
                            { "estimatedSizeMB", Math.Round(groupSizeMB, 1) },
                            { "message", $"Group '{gName}' estimated size {groupSizeMB:F1} MB (consider splitting)" }
                        });
                    }
                }
            }

            // Detect duplicates
            foreach (var kvp in allEntries)
            {
                if (kvp.Value.Count > 1)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(kvp.Key);
                    issues.Add(new Dictionary<string, object>
                    {
                        { "type", "duplicate_entry" },
                        { "severity", "warning" },
                        { "guid", kvp.Key },
                        { "assetPath", assetPath },
                        { "groups", kvp.Value },
                        { "message", $"Asset '{assetPath}' appears in {kvp.Value.Count} groups: {string.Join(", ", kvp.Value)}" }
                    });
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "totalEntries", totalEntries },
                { "issueCount", issues.Count },
                { "issues", issues },
                { "errors", issues.Count(i => (i as Dictionary<string, object>)?["severity"]?.ToString() == "error") },
                { "warnings", issues.Count(i => (i as Dictionary<string, object>)?["severity"]?.ToString() == "warning") }
            };
        }
    }
}
