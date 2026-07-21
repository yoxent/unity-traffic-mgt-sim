using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace UnityMcpPro
{
    public class PackageCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("list_packages", ListPackages);
            router.Register("add_package", AddPackage);
            router.Register("remove_package", RemovePackage);
            router.Register("search_packages", SearchPackages);
            router.Register("list_asset_store_cache", ListAssetStoreCache);
            router.Register("import_unitypackage", ImportUnityPackage);
        }

        private static object ListPackages(Dictionary<string, object> p)
        {
            var request = Client.List(true);
            WaitForRequest(request);

            if (request.Status == StatusCode.Failure)
                throw new Exception($"Failed to list packages: {request.Error?.message}");

            var packages = new List<object>();
            foreach (var pkg in request.Result)
            {
                packages.Add(new Dictionary<string, object>
                {
                    { "name", pkg.name },
                    { "version", pkg.version },
                    { "displayName", pkg.displayName },
                    { "description", pkg.description ?? "" },
                    { "source", pkg.source.ToString() }
                });
            }

            return new Dictionary<string, object>
            {
                { "count", packages.Count },
                { "packages", packages }
            };
        }

        private static object AddPackage(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_package");
            string identifier = GetStringParam(p, "identifier");
            if (string.IsNullOrEmpty(identifier))
                throw new ArgumentException("identifier is required (e.g. 'com.unity.textmeshpro' or 'com.unity.textmeshpro@3.0.6')");

            var request = Client.Add(identifier);
            WaitForRequest(request);

            if (request.Status == StatusCode.Failure)
                throw new Exception($"Failed to add package: {request.Error?.message}");

            var pkg = request.Result;
            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", pkg.name },
                { "version", pkg.version },
                { "displayName", pkg.displayName }
            };
        }

        private static object RemovePackage(Dictionary<string, object> p)
        {
            ThrowIfPlaying("remove_package");
            string packageName = GetStringParam(p, "name");
            if (string.IsNullOrEmpty(packageName))
                throw new ArgumentException("name is required (e.g. 'com.unity.textmeshpro')");

            var request = Client.Remove(packageName);
            WaitForRequest(request);

            if (request.Status == StatusCode.Failure)
                throw new Exception($"Failed to remove package: {request.Error?.message}");

            return Success($"Removed package: {packageName}");
        }

        private static object SearchPackages(Dictionary<string, object> p)
        {
            string query = GetStringParam(p, "query");

            SearchRequest request;
            if (string.IsNullOrEmpty(query))
                request = Client.SearchAll();
            else
                request = Client.Search(query);

            WaitForRequest(request);

            if (request.Status == StatusCode.Failure)
                throw new Exception($"Search failed: {request.Error?.message}");

            var packages = new List<object>();
            foreach (var pkg in request.Result)
            {
                packages.Add(new Dictionary<string, object>
                {
                    { "name", pkg.name },
                    { "version", pkg.versions.latest },
                    { "displayName", pkg.displayName },
                    { "description", pkg.description ?? "" }
                });
            }

            return new Dictionary<string, object>
            {
                { "count", packages.Count },
                { "packages", packages }
            };
        }

        private static object ListAssetStoreCache(Dictionary<string, object> p)
        {
            string filter = GetStringParam(p, "filter", "");

            // Asset Store cache locations
            string cachePath;
            if (Application.platform == RuntimePlatform.WindowsEditor)
                cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unity", "Asset Store-5.x");
            else
                cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Unity", "Asset Store-5.x");

            if (!Directory.Exists(cachePath))
                return new Dictionary<string, object>
                {
                    { "count", 0 },
                    { "cachePath", cachePath },
                    { "packages", new List<object>() },
                    { "message", "Asset Store cache directory not found" }
                };

            var files = Directory.GetFiles(cachePath, "*.unitypackage", SearchOption.AllDirectories);
            var packages = new List<object>();

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                string relativePath = file.Substring(cachePath.Length + 1);

                if (!string.IsNullOrEmpty(filter) &&
                    !relativePath.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                    continue;

                packages.Add(new Dictionary<string, object>
                {
                    { "path", file },
                    { "relativePath", relativePath },
                    { "name", Path.GetFileNameWithoutExtension(file) },
                    { "sizeMB", Math.Round(info.Length / 1048576.0, 2) },
                    { "lastModified", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") }
                });
            }

            // Sort by last modified descending
            packages.Sort((a, b) =>
            {
                var aDate = ((Dictionary<string, object>)a)["lastModified"].ToString();
                var bDate = ((Dictionary<string, object>)b)["lastModified"].ToString();
                return string.Compare(bDate, aDate, StringComparison.Ordinal);
            });

            return new Dictionary<string, object>
            {
                { "count", packages.Count },
                { "cachePath", cachePath },
                { "packages", packages }
            };
        }

        private static object ImportUnityPackage(Dictionary<string, object> p)
        {
            ThrowIfPlaying("import_unitypackage");
            string packagePath = GetStringParam(p, "path");
            if (string.IsNullOrEmpty(packagePath))
                throw new ArgumentException("path is required (full path to .unitypackage file)");

            if (!File.Exists(packagePath))
                throw new FileNotFoundException($"Package file not found: {packagePath}");

            AssetDatabase.ImportPackage(packagePath, false);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", $"Imported package: {Path.GetFileName(packagePath)}" },
                { "path", packagePath }
            };
        }

        private static void WaitForRequest(Request request)
        {
            int maxIterations = 3000; // ~30 seconds at 10ms intervals
            int iterations = 0;
            while (!request.IsCompleted && iterations < maxIterations)
            {
                System.Threading.Thread.Sleep(10);
                iterations++;
            }

            if (!request.IsCompleted)
                throw new TimeoutException("Package manager request timed out after 30 seconds");
        }
    }
}
