using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class LocalizationCommands : BaseCommand
    {
        // All access via reflection since com.unity.localization may not be installed
        private static bool _typesResolved;
        private static Type _localizationEditorSettingsType;
        private static Type _localizationSettingsType;
        private static Type _stringTableCollectionType;
        private static Type _stringTableType;
        private static Type _sharedTableDataType;
        private static Type _localeType;
        private static Type _localeIdentifierType;

        public static void Register(CommandRouter router)
        {
            router.Register("create_string_table", CreateStringTable);
            router.Register("add_locale_entry", AddLocaleEntry);
            router.Register("get_locale_entries", GetLocaleEntries);
            router.Register("set_project_locale", SetProjectLocale);
        }

        private static bool ResolveTypes()
        {
            if (_typesResolved) return _localizationEditorSettingsType != null;
            _typesResolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (_localizationEditorSettingsType == null)
                        _localizationEditorSettingsType = asm.GetType("UnityEditor.Localization.LocalizationEditorSettings");
                    if (_localizationSettingsType == null)
                        _localizationSettingsType = asm.GetType("UnityEngine.Localization.Settings.LocalizationSettings");
                    if (_stringTableCollectionType == null)
                        _stringTableCollectionType = asm.GetType("UnityEditor.Localization.StringTableCollection");
                    if (_stringTableType == null)
                        _stringTableType = asm.GetType("UnityEngine.Localization.Tables.StringTable");
                    if (_sharedTableDataType == null)
                        _sharedTableDataType = asm.GetType("UnityEngine.Localization.Tables.SharedTableData");
                    if (_localeType == null)
                        _localeType = asm.GetType("UnityEngine.Localization.Locale");
                    if (_localeIdentifierType == null)
                        _localeIdentifierType = asm.GetType("UnityEngine.Localization.LocaleIdentifier");
                }
                catch { }
            }

            return _localizationEditorSettingsType != null;
        }

        private static void EnsurePackageAvailable()
        {
            if (!ResolveTypes())
                throw new InvalidOperationException("Localization package is not installed. Install com.unity.localization via Package Manager.");
        }

        private static object CreateStringTable(Dictionary<string, object> p)
        {
            string name = GetStringParam(p, "name");
            string[] locales = GetStringListParam(p, "locales");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");
            if (locales == null || locales.Length == 0)
                throw new ArgumentException("locales is required and must be non-empty");

            EnsurePackageAvailable();

            // Ensure locales exist
            var createdLocales = new List<string>();
            foreach (var localeCode in locales)
            {
                EnsureLocaleExists(localeCode);
                createdLocales.Add(localeCode);
            }

            // Create StringTableCollection
            // Use LocalizationEditorSettings.CreateStringTableCollection(name, assetPath, locales)
            var createMethod = _localizationEditorSettingsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateStringTableCollection" &&
                    m.GetParameters().Length >= 2);

            if (createMethod != null)
            {
                string savePath = $"Assets/Localization/Tables";
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(Application.dataPath, "Localization/Tables")))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Localization/Tables"));
                    AssetDatabase.Refresh();
                }

                try
                {
                    var paramInfos = createMethod.GetParameters();
                    object collection;
                    if (paramInfos.Length == 3)
                    {
                        collection = createMethod.Invoke(null, new object[] { name, savePath, GetLocaleList(locales) });
                    }
                    else
                    {
                        collection = createMethod.Invoke(null, new object[] { name, savePath });
                    }

                    AssetDatabase.SaveAssets();

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "tableName", name },
                        { "locales", createdLocales },
                        { "path", savePath },
                        { "message", $"String Table Collection '{name}' created with locales: {string.Join(", ", createdLocales)}" }
                    };
                }
                catch (TargetInvocationException ex)
                {
                    throw new InvalidOperationException($"Failed to create string table: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            throw new InvalidOperationException("CreateStringTableCollection method not found in LocalizationEditorSettings");
        }

        private static object AddLocaleEntry(Dictionary<string, object> p)
        {
            string tableName = GetStringParam(p, "table_name");
            string key = GetStringParam(p, "key");
            var values = GetDictParam(p, "values");

            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("table_name is required");
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("key is required");
            if (values == null || values.Count == 0)
                throw new ArgumentException("values is required (e.g. {\"en\": \"Hello\", \"ja\": \"こんにちは\"})");

            EnsurePackageAvailable();

            // Find the StringTableCollection
            var collection = FindStringTableCollection(tableName);
            if (collection == null)
                throw new ArgumentException($"String Table Collection '{tableName}' not found");

            // Get SharedTableData to add the key
            var sharedDataProp = _stringTableCollectionType.GetProperty("SharedData",
                BindingFlags.Public | BindingFlags.Instance);
            var sharedData = sharedDataProp?.GetValue(collection);

            if (sharedData != null)
            {
                // Add entry to shared data
                var addEntryMethod = _sharedTableDataType.GetMethod("AddKey",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { typeof(string) }, null);

                if (addEntryMethod == null)
                {
                    addEntryMethod = _sharedTableDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "AddKey" && m.GetParameters().Length == 1);
                }

                addEntryMethod?.Invoke(sharedData, new object[] { key });
            }

            // Set locale-specific values
            var getTableMethod = _stringTableCollectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetTable" || m.Name == "GetStringTable");

            // Alternative: iterate StringTables property
            var stringTablesProp = _stringTableCollectionType.GetProperty("StringTables",
                BindingFlags.Public | BindingFlags.Instance);
            if (stringTablesProp == null)
            {
                stringTablesProp = _stringTableCollectionType.GetProperty("Tables",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            var updatedLocales = new List<string>();

            if (stringTablesProp != null)
            {
                var tables = stringTablesProp.GetValue(collection) as System.Collections.IEnumerable;
                if (tables != null)
                {
                    foreach (var table in tables)
                    {
                        if (table == null) continue;

                        // Get locale identifier from table
                        var localeIdProp = table.GetType().GetProperty("LocaleIdentifier",
                            BindingFlags.Public | BindingFlags.Instance);
                        var localeId = localeIdProp?.GetValue(table);
                        string code = localeId?.ToString() ?? "";

                        // Also check Code property
                        if (_localeIdentifierType != null && localeId != null)
                        {
                            var codeProp = _localeIdentifierType.GetProperty("Code",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (codeProp != null)
                                code = codeProp.GetValue(localeId)?.ToString() ?? code;
                        }

                        // Find matching value
                        string matchingValue = null;
                        foreach (var kvp in values)
                        {
                            if (code.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                                kvp.Key.Equals(code, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingValue = kvp.Value?.ToString();
                                break;
                            }
                        }

                        if (matchingValue != null)
                        {
                            // AddEntry or set value
                            var addEntryMethod = table.GetType().GetMethod("AddEntry",
                                BindingFlags.Public | BindingFlags.Instance,
                                null, new Type[] { typeof(string), typeof(string) }, null);

                            if (addEntryMethod != null)
                            {
                                addEntryMethod.Invoke(table, new object[] { key, matchingValue });
                                updatedLocales.Add(code);
                            }

                            EditorUtility.SetDirty(table as UnityEngine.Object);
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "tableName", tableName },
                { "key", key },
                { "updatedLocales", updatedLocales },
                { "message", $"Entry '{key}' added/updated in '{tableName}' for locales: {string.Join(", ", updatedLocales)}" }
            };
        }

        private static object GetLocaleEntries(Dictionary<string, object> p)
        {
            string tableName = GetStringParam(p, "table_name");
            string filterLocale = GetStringParam(p, "locale");

            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("table_name is required");

            EnsurePackageAvailable();

            var collection = FindStringTableCollection(tableName);
            if (collection == null)
                throw new ArgumentException($"String Table Collection '{tableName}' not found");

            // Get shared data for keys
            var sharedDataProp = _stringTableCollectionType.GetProperty("SharedData",
                BindingFlags.Public | BindingFlags.Instance);
            var sharedData = sharedDataProp?.GetValue(collection);

            var allKeys = new List<string>();
            if (sharedData != null)
            {
                var entriesProp = _sharedTableDataType.GetProperty("Entries",
                    BindingFlags.Public | BindingFlags.Instance);
                var entries = entriesProp?.GetValue(sharedData) as System.Collections.IList;
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        var keyProp = entry.GetType().GetProperty("Key",
                            BindingFlags.Public | BindingFlags.Instance);
                        string k = keyProp?.GetValue(entry)?.ToString();
                        if (!string.IsNullOrEmpty(k))
                            allKeys.Add(k);
                    }
                }
            }

            // Get values per locale
            var result = new Dictionary<string, object>();
            var stringTablesProp = _stringTableCollectionType.GetProperty("StringTables",
                BindingFlags.Public | BindingFlags.Instance)
                ?? _stringTableCollectionType.GetProperty("Tables",
                    BindingFlags.Public | BindingFlags.Instance);

            var localeData = new List<object>();

            if (stringTablesProp != null)
            {
                var tables = stringTablesProp.GetValue(collection) as System.Collections.IEnumerable;
                if (tables != null)
                {
                    foreach (var table in tables)
                    {
                        if (table == null) continue;

                        var localeIdProp = table.GetType().GetProperty("LocaleIdentifier",
                            BindingFlags.Public | BindingFlags.Instance);
                        var localeId = localeIdProp?.GetValue(table);
                        string code = localeId?.ToString() ?? "";

                        if (_localeIdentifierType != null && localeId != null)
                        {
                            var codeProp = _localeIdentifierType.GetProperty("Code",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (codeProp != null)
                                code = codeProp.GetValue(localeId)?.ToString() ?? code;
                        }

                        // Filter by locale if specified
                        if (!string.IsNullOrEmpty(filterLocale) &&
                            !code.StartsWith(filterLocale, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var entriesDict = new Dictionary<string, object>();
                        var getEntryMethod = table.GetType().GetMethod("GetEntry",
                            BindingFlags.Public | BindingFlags.Instance,
                            null, new Type[] { typeof(string) }, null);

                        foreach (var key in allKeys)
                        {
                            try
                            {
                                var entry = getEntryMethod?.Invoke(table, new object[] { key });
                                if (entry != null)
                                {
                                    var valueProp = entry.GetType().GetProperty("Value",
                                        BindingFlags.Public | BindingFlags.Instance)
                                        ?? entry.GetType().GetProperty("LocalizedValue",
                                            BindingFlags.Public | BindingFlags.Instance);
                                    entriesDict[key] = valueProp?.GetValue(entry)?.ToString() ?? "";
                                }
                                else
                                {
                                    entriesDict[key] = "";
                                }
                            }
                            catch
                            {
                                entriesDict[key] = "[error]";
                            }
                        }

                        localeData.Add(new Dictionary<string, object>
                        {
                            { "locale", code },
                            { "entries", entriesDict }
                        });
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "tableName", tableName },
                { "keyCount", allKeys.Count },
                { "keys", allKeys },
                { "locales", localeData }
            };
        }

        private static object SetProjectLocale(Dictionary<string, object> p)
        {
            string defaultLocale = GetStringParam(p, "default_locale");
            string[] addLocales = GetStringListParam(p, "add_locales");
            string[] removeLocales = GetStringListParam(p, "remove_locales");

            EnsurePackageAvailable();

            var changes = new List<string>();

            // Add locales
            if (addLocales != null)
            {
                foreach (var code in addLocales)
                {
                    EnsureLocaleExists(code);
                    changes.Add($"Added locale: {code}");
                }
            }

            // Remove locales
            if (removeLocales != null)
            {
                var removeMethod = _localizationEditorSettingsType.GetMethod("RemoveLocale",
                    BindingFlags.Public | BindingFlags.Static);

                if (removeMethod != null)
                {
                    var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                        BindingFlags.Public | BindingFlags.Static);
                    var localesList = getLocalesMethod?.Invoke(null, null) as System.Collections.IList;

                    if (localesList != null)
                    {
                        foreach (var code in removeLocales)
                        {
                            foreach (var locale in localesList)
                            {
                                var idProp = _localeType.GetProperty("Identifier",
                                    BindingFlags.Public | BindingFlags.Instance);
                                var id = idProp?.GetValue(locale);
                                var codeProp = _localeIdentifierType?.GetProperty("Code",
                                    BindingFlags.Public | BindingFlags.Instance);
                                string localeCode = codeProp?.GetValue(id)?.ToString() ?? "";

                                if (localeCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                                {
                                    removeMethod.Invoke(null, new object[] { locale, true });
                                    changes.Add($"Removed locale: {code}");
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Set default locale
            if (!string.IsNullOrEmpty(defaultLocale))
            {
                // Get the LocalizationSettings instance
                var getSettingsMethod = _localizationSettingsType?.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);

                // Find the locale object for the default code
                var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                    BindingFlags.Public | BindingFlags.Static);
                var localesList = getLocalesMethod?.Invoke(null, null) as System.Collections.IList;

                if (localesList != null)
                {
                    foreach (var locale in localesList)
                    {
                        var idProp = _localeType.GetProperty("Identifier",
                            BindingFlags.Public | BindingFlags.Instance);
                        var id = idProp?.GetValue(locale);
                        var codeProp = _localeIdentifierType?.GetProperty("Code",
                            BindingFlags.Public | BindingFlags.Instance);
                        string localeCode = codeProp?.GetValue(id)?.ToString() ?? "";

                        if (localeCode.Equals(defaultLocale, StringComparison.OrdinalIgnoreCase))
                        {
                            // Set as project locale via settings
                            var settingsInstance = getSettingsMethod?.GetValue(null);
                            if (settingsInstance != null)
                            {
                                var selectedLocaleProp = _localizationSettingsType.GetProperty("SelectedLocale",
                                    BindingFlags.Public | BindingFlags.Instance);
                                selectedLocaleProp?.SetValue(settingsInstance, locale);
                                EditorUtility.SetDirty(settingsInstance as UnityEngine.Object);
                            }
                            changes.Add($"Set default locale: {defaultLocale}");
                            break;
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();

            // Get current locale list
            var currentLocales = new List<string>();
            var getLocalesMethod2 = _localizationEditorSettingsType.GetMethod("GetLocales",
                BindingFlags.Public | BindingFlags.Static);
            var allLocales = getLocalesMethod2?.Invoke(null, null) as System.Collections.IList;
            if (allLocales != null)
            {
                foreach (var locale in allLocales)
                {
                    var idProp = _localeType.GetProperty("Identifier",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = idProp?.GetValue(locale);
                    var codeProp = _localeIdentifierType?.GetProperty("Code",
                        BindingFlags.Public | BindingFlags.Instance);
                    currentLocales.Add(codeProp?.GetValue(id)?.ToString() ?? "");
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "changes", changes },
                { "currentLocales", currentLocales },
                { "message", changes.Count > 0
                    ? $"Locale settings updated: {string.Join("; ", changes)}"
                    : "No changes applied" }
            };
        }

        // Helper: ensure a locale asset exists
        private static void EnsureLocaleExists(string code)
        {
            // Check if locale already exists
            var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                BindingFlags.Public | BindingFlags.Static);
            var localesList = getLocalesMethod?.Invoke(null, null) as System.Collections.IList;

            if (localesList != null)
            {
                foreach (var locale in localesList)
                {
                    var idProp = _localeType.GetProperty("Identifier",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = idProp?.GetValue(locale);
                    var codeProp = _localeIdentifierType?.GetProperty("Code",
                        BindingFlags.Public | BindingFlags.Instance);
                    string existingCode = codeProp?.GetValue(id)?.ToString() ?? "";
                    if (existingCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                        return; // Already exists
                }
            }

            // Create locale
            var createLocaleMethod = _localeType.GetMethod("CreateLocale",
                BindingFlags.Public | BindingFlags.Static);

            if (createLocaleMethod == null)
            {
                // Alternative: instantiate and set identifier
                var localeObj = ScriptableObject.CreateInstance(_localeType);

                // Construct LocaleIdentifier from code
                var idCtor = _localeIdentifierType.GetConstructor(new Type[] { typeof(string) });
                if (idCtor != null)
                {
                    var localeId = idCtor.Invoke(new object[] { code });
                    var idProp = _localeType.GetProperty("Identifier",
                        BindingFlags.Public | BindingFlags.Instance);
                    idProp?.SetValue(localeObj, localeId);
                }

                // Save as asset
                string dir = System.IO.Path.Combine(Application.dataPath, "Localization/Locales");
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                string assetPath = $"Assets/Localization/Locales/{code}.asset";
                AssetDatabase.CreateAsset(localeObj, assetPath);

                // Add to settings
                var addLocaleMethod = _localizationEditorSettingsType.GetMethod("AddLocale",
                    BindingFlags.Public | BindingFlags.Static);
                addLocaleMethod?.Invoke(null, new object[] { localeObj, true });
            }
            else
            {
                // Use static factory
                var idCtor = _localeIdentifierType.GetConstructor(new Type[] { typeof(string) });
                if (idCtor != null)
                {
                    var localeId = idCtor.Invoke(new object[] { code });
                    var localeObj = createLocaleMethod.Invoke(null, new object[] { localeId });

                    string dir = System.IO.Path.Combine(Application.dataPath, "Localization/Locales");
                    if (!System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);

                    string assetPath = $"Assets/Localization/Locales/{code}.asset";
                    AssetDatabase.CreateAsset(localeObj as UnityEngine.Object, assetPath);

                    var addLocaleMethod = _localizationEditorSettingsType.GetMethod("AddLocale",
                        BindingFlags.Public | BindingFlags.Static);
                    addLocaleMethod?.Invoke(null, new object[] { localeObj, true });
                }
            }

            AssetDatabase.SaveAssets();
        }

        // Helper: find a StringTableCollection by name
        private static object FindStringTableCollection(string name)
        {
            var getCollectionsMethod = _localizationEditorSettingsType.GetMethod("GetStringTableCollections",
                BindingFlags.Public | BindingFlags.Static);

            if (getCollectionsMethod == null)
            {
                // Try alternative approach via AssetDatabase
                var guids = AssetDatabase.FindAssets($"t:StringTableCollection {name}");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, _stringTableCollectionType);
                    if (asset != null)
                    {
                        var nameProp = _stringTableCollectionType.GetProperty("TableCollectionName",
                            BindingFlags.Public | BindingFlags.Instance)
                            ?? _stringTableCollectionType.GetProperty("name",
                                BindingFlags.Public | BindingFlags.Instance);
                        string collName = nameProp?.GetValue(asset)?.ToString() ?? "";
                        if (collName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            return asset;
                    }
                }
                return null;
            }

            var collections = getCollectionsMethod.Invoke(null, null) as System.Collections.IEnumerable;
            if (collections != null)
            {
                foreach (var coll in collections)
                {
                    var nameProp = _stringTableCollectionType.GetProperty("TableCollectionName",
                        BindingFlags.Public | BindingFlags.Instance)
                        ?? _stringTableCollectionType.GetProperty("name",
                            BindingFlags.Public | BindingFlags.Instance);
                    string collName = nameProp?.GetValue(coll)?.ToString() ?? "";
                    if (collName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return coll;
                }
            }

            return null;
        }

        // Helper: get IList<Locale> from locale codes
        private static object GetLocaleList(string[] codes)
        {
            var listType = typeof(List<>).MakeGenericType(_localeType);
            var list = Activator.CreateInstance(listType) as System.Collections.IList;

            var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                BindingFlags.Public | BindingFlags.Static);
            var allLocales = getLocalesMethod?.Invoke(null, null) as System.Collections.IList;

            if (allLocales != null && list != null)
            {
                foreach (var locale in allLocales)
                {
                    var idProp = _localeType.GetProperty("Identifier",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = idProp?.GetValue(locale);
                    var codeProp = _localeIdentifierType?.GetProperty("Code",
                        BindingFlags.Public | BindingFlags.Instance);
                    string localeCode = codeProp?.GetValue(id)?.ToString() ?? "";

                    if (codes.Any(c => c.Equals(localeCode, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(locale);
                    }
                }
            }

            return list;
        }
    }
}
