using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcpPro
{
    public class OptimizationCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("analyze_draw_calls", AnalyzeDrawCalls);
            router.Register("generate_lod_group", GenerateLodGroup);
            router.Register("analyze_textures", AnalyzeTextures);
            router.Register("optimize_mesh", OptimizeMesh);
            router.Register("get_rendering_stats", GetRenderingStats);
            router.Register("suggest_optimizations", SuggestOptimizations);
            router.Register("analyze_overdraw", AnalyzeOverdraw);
        }

        private static object AnalyzeDrawCalls(Dictionary<string, object> p)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            int totalRenderers = 0;
            int staticBatchCandidates = 0;
            int dynamicBatchCandidates = 0;
            int alreadyStaticBatched = 0;
            int estimatedDrawCalls = 0;
            var materialSet = new HashSet<Material>();
            var materialUsage = new Dictionary<string, int>();
            var batchingSuggestions = new List<object>();

            foreach (var root in rootObjects)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    totalRenderers++;

                    bool isStatic = GameObjectUtility.AreStaticEditorFlagsSet(
                        renderer.gameObject, StaticEditorFlags.BatchingStatic);

                    if (isStatic)
                        alreadyStaticBatched++;

                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) continue;
                        materialSet.Add(mat);
                        string matName = mat.name;
                        if (!materialUsage.ContainsKey(matName))
                            materialUsage[matName] = 0;
                        materialUsage[matName]++;

                        if (!isStatic)
                            estimatedDrawCalls++;
                    }

                    // Static batching candidate: not marked static, has MeshRenderer, not skinned
                    if (!isStatic && renderer is MeshRenderer)
                    {
                        var mf = renderer.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            staticBatchCandidates++;

                            // Dynamic batching candidate: < 300 vertices
                            if (mf.sharedMesh.vertexCount < 300)
                                dynamicBatchCandidates++;
                        }
                    }
                }
            }

            // Identify materials used on many non-static objects (good batching candidates)
            foreach (var root in rootObjects)
            {
                var renderersByMat = new Dictionary<Material, List<string>>();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    bool isStatic = GameObjectUtility.AreStaticEditorFlagsSet(
                        renderer.gameObject, StaticEditorFlags.BatchingStatic);
                    if (isStatic) continue;

                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) continue;
                        if (!renderersByMat.ContainsKey(mat))
                            renderersByMat[mat] = new List<string>();
                        if (renderersByMat[mat].Count < 5)
                            renderersByMat[mat].Add(GetGameObjectPath(renderer.gameObject));
                    }
                }

                foreach (var kvp in renderersByMat)
                {
                    if (kvp.Value.Count >= 3)
                    {
                        batchingSuggestions.Add(new Dictionary<string, object>
                        {
                            { "material", kvp.Key.name },
                            { "nonStaticCount", kvp.Value.Count },
                            { "suggestion", "Mark these objects as Batching Static to reduce draw calls" },
                            { "sampleObjects", kvp.Value }
                        });
                    }
                }
            }

            // Top materials by usage
            var topMaterials = materialUsage.OrderByDescending(x => x.Value).Take(10)
                .Select(kvp => new Dictionary<string, object>
                {
                    { "name", kvp.Key },
                    { "usageCount", kvp.Value }
                }).ToList();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "totalRenderers", totalRenderers },
                { "uniqueMaterials", materialSet.Count },
                { "estimatedDrawCalls", estimatedDrawCalls },
                { "alreadyStaticBatched", alreadyStaticBatched },
                { "staticBatchCandidates", staticBatchCandidates },
                { "dynamicBatchCandidates", dynamicBatchCandidates },
                { "topMaterials", topMaterials },
                { "batchingSuggestions", batchingSuggestions }
            };
        }

        private static object GenerateLodGroup(Dictionary<string, object> p)
        {
            string targetPath = GetStringParam(p, "target");
            int lodCount = GetIntParam(p, "lod_count", 3);

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);

            // Parse transitions
            float[] transitions;
            var transArr = GetStringListParam(p, "transitions");
            if (transArr != null && transArr.Length > 0)
            {
                transitions = new float[transArr.Length];
                for (int i = 0; i < transArr.Length; i++)
                {
                    if (float.TryParse(transArr[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val))
                        transitions[i] = val;
                }
                lodCount = transitions.Length;
            }
            else
            {
                // Generate default transitions
                transitions = new float[lodCount];
                for (int i = 0; i < lodCount; i++)
                {
                    transitions[i] = 1.0f / (i + 1) * 0.6f;
                }
            }

            // Also handle transitions passed as List<object> (from JSON array of numbers)
            if (p.TryGetValue("transitions", out var transObj) && transObj is List<object> transList)
            {
                transitions = new float[transList.Count];
                for (int i = 0; i < transList.Count; i++)
                {
                    if (transObj is double d)
                        transitions[i] = (float)d;
                    else if (transList[i] is double dd)
                        transitions[i] = (float)dd;
                    else if (transList[i] is long l)
                        transitions[i] = l;
                    else if (float.TryParse(transList[i].ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                        transitions[i] = parsed;
                }
                lodCount = transitions.Length;
            }

            RecordUndo(go, "Generate LOD Group");

            var lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = go.AddComponent<LODGroup>();

            // Get existing renderers for LOD0
            var renderers = go.GetComponentsInChildren<Renderer>();

            var lods = new LOD[lodCount];
            for (int i = 0; i < lodCount; i++)
            {
                float screenHeight = i < transitions.Length ? transitions[i] : 0.01f;
                // LOD0 gets all current renderers; lower LODs get empty renderer arrays (user assigns later)
                lods[i] = new LOD(screenHeight, i == 0 ? renderers : new Renderer[0]);
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            var lodInfo = new List<object>();
            for (int i = 0; i < lodCount; i++)
            {
                lodInfo.Add(new Dictionary<string, object>
                {
                    { "level", i },
                    { "screenHeight", transitions[i] },
                    { "rendererCount", i == 0 ? renderers.Length : 0 }
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", GetGameObjectPath(go) },
                { "lodCount", lodCount },
                { "lods", lodInfo },
                { "message", lodCount > 1
                    ? "LOD Group created. LOD0 uses existing renderers. Assign meshes to lower LOD levels manually."
                    : "LOD Group created with 1 level." }
            };
        }

        private static object AnalyzeTextures(Dictionary<string, object> p)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            var textures = new List<object>();
            var issues = new List<object>();
            long totalSize = 0;
            int oversizedCount = 0;
            int npotCount = 0;
            int readWriteCount = 0;
            int uncompressedCount = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                long fileSize = 0;
                try
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Exists)
                        fileSize = fileInfo.Length;
                }
                catch { }
                totalSize += fileSize;

                int width = tex.width;
                int height = tex.height;
                bool isNpot = !IsPowerOfTwo(width) || !IsPowerOfTwo(height);
                bool isOversized = width > 2048 || height > 2048;
                bool isReadWrite = importer.isReadable;

                // Get platform-specific settings for compression check
                var defaultSettings = importer.GetDefaultPlatformTextureSettings();
                bool isUncompressed = defaultSettings.format == TextureImporterFormat.RGBA32 ||
                                      defaultSettings.format == TextureImporterFormat.RGB24 ||
                                      defaultSettings.format == TextureImporterFormat.ARGB32;

                var texIssues = new List<string>();
                if (isOversized) { oversizedCount++; texIssues.Add("Oversized (>2048)"); }
                if (isNpot) { npotCount++; texIssues.Add("Non-power-of-2"); }
                if (isReadWrite) { readWriteCount++; texIssues.Add("Read/Write enabled"); }
                if (isUncompressed) { uncompressedCount++; texIssues.Add("Uncompressed format"); }

                if (texIssues.Count > 0)
                {
                    issues.Add(new Dictionary<string, object>
                    {
                        { "path", path },
                        { "size", $"{width}x{height}" },
                        { "format", defaultSettings.format.ToString() },
                        { "fileSizeKB", Math.Round(fileSize / 1024.0, 1) },
                        { "readWrite", isReadWrite },
                        { "mipmaps", importer.mipmapEnabled },
                        { "issues", texIssues }
                    });
                }
            }

            var suggestions = new List<string>();
            if (oversizedCount > 0)
                suggestions.Add($"Reduce {oversizedCount} textures larger than 2048px to improve memory usage");
            if (npotCount > 0)
                suggestions.Add($"Convert {npotCount} NPOT textures to power-of-2 for better GPU compatibility");
            if (readWriteCount > 0)
                suggestions.Add($"Disable Read/Write on {readWriteCount} textures to halve their memory footprint");
            if (uncompressedCount > 0)
                suggestions.Add($"Compress {uncompressedCount} uncompressed textures to reduce build size");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "totalTextures", guids.Length },
                { "totalSizeMB", Math.Round(totalSize / (1024.0 * 1024.0), 2) },
                { "oversized", oversizedCount },
                { "nonPowerOf2", npotCount },
                { "readWriteEnabled", readWriteCount },
                { "uncompressed", uncompressedCount },
                { "issueCount", issues.Count },
                { "issues", issues },
                { "suggestions", suggestions }
            };
        }

        private static bool IsPowerOfTwo(int x)
        {
            return x > 0 && (x & (x - 1)) == 0;
        }

        private static object OptimizeMesh(Dictionary<string, object> p)
        {
            string specificPath = GetStringParam(p, "path");
            var meshInfos = new List<object>();
            var issues = new List<object>();
            int totalVertices = 0;
            int totalTriangles = 0;

            if (!string.IsNullOrEmpty(specificPath))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(specificPath);
                if (mesh == null)
                    throw new ArgumentException($"Mesh not found at: {specificPath}");

                var info = AnalyzeSingleMesh(specificPath, mesh);
                meshInfos.Add(info);
                totalVertices += mesh.vertexCount;
                totalTriangles += mesh.triangles.Length / 3;
            }
            else
            {
                string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets" });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null) continue;

                    totalVertices += mesh.vertexCount;
                    totalTriangles += mesh.triangles.Length / 3;

                    var info = AnalyzeSingleMesh(path, mesh);
                    meshInfos.Add(info);

                    // Check for issues
                    var meshIssues = new List<string>();
                    if (mesh.vertexCount > 65535)
                        meshIssues.Add("High vertex count (>65K, cannot use 16-bit index buffer)");
                    if (mesh.isReadable)
                        meshIssues.Add("Read/Write enabled (doubles memory)");
                    if (mesh.subMeshCount > 1)
                        meshIssues.Add($"Multiple submeshes ({mesh.subMeshCount}) increase draw calls");

                    if (meshIssues.Count > 0)
                    {
                        issues.Add(new Dictionary<string, object>
                        {
                            { "path", path },
                            { "vertices", mesh.vertexCount },
                            { "issues", meshIssues }
                        });
                    }
                }
            }

            var suggestions = new List<string>();
            int readWriteCount = meshInfos.Count(m =>
                m is Dictionary<string, object> d && d.ContainsKey("readWrite") && (bool)d["readWrite"]);
            int highPolyCount = meshInfos.Count(m =>
                m is Dictionary<string, object> d && d.ContainsKey("vertices") && (int)d["vertices"] > 10000);

            if (readWriteCount > 0)
                suggestions.Add($"Disable Read/Write on {readWriteCount} meshes to reduce memory");
            if (highPolyCount > 0)
                suggestions.Add($"{highPolyCount} meshes have >10K vertices. Consider LOD groups or mesh decimation.");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "totalMeshes", meshInfos.Count },
                { "totalVertices", totalVertices },
                { "totalTriangles", totalTriangles },
                { "issueCount", issues.Count },
                { "meshes", meshInfos },
                { "issues", issues },
                { "suggestions", suggestions }
            };
        }

        private static Dictionary<string, object> AnalyzeSingleMesh(string path, Mesh mesh)
        {
            return new Dictionary<string, object>
            {
                { "path", path },
                { "name", mesh.name },
                { "vertices", mesh.vertexCount },
                { "triangles", mesh.triangles.Length / 3 },
                { "subMeshCount", mesh.subMeshCount },
                { "readWrite", mesh.isReadable },
                { "hasNormals", mesh.normals != null && mesh.normals.Length > 0 },
                { "hasTangents", mesh.tangents != null && mesh.tangents.Length > 0 },
                { "hasUV", mesh.uv != null && mesh.uv.Length > 0 },
                { "hasUV2", mesh.uv2 != null && mesh.uv2.Length > 0 },
                { "bounds", $"center:{mesh.bounds.center}, size:{mesh.bounds.size}" }
            };
        }

        private static object GetRenderingStats(Dictionary<string, object> p)
        {
            // UnityStats is only available when the Game view is rendering
            try
            {
                var statsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UnityStats");
                if (statsType == null)
                    throw new Exception("UnityStats type not found");

                var result = new Dictionary<string, object>();

                var props = new[] { "batches", "drawCalls", "triangles", "vertices",
                    "setPassCalls", "shadowCasters", "renderTextureCount",
                    "renderTextureBytes", "usedTextureCount", "usedTextureBytes" };

                foreach (var propName in props)
                {
                    var prop = statsType.GetProperty(propName,
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (prop != null)
                    {
                        try { result[propName] = prop.GetValue(null); }
                        catch { result[propName] = "N/A"; }
                    }
                }

                // Also get string-based stats for formatted display
                var screenResStr = statsType.GetProperty("screenRes",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (screenResStr != null)
                {
                    try { result["screenRes"] = screenResStr.GetValue(null); }
                    catch { }
                }

                result["success"] = true;
                result["note"] = "Stats reflect the last rendered Game view frame. Open Game view for accurate data.";
                return result;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "warning", $"Could not access UnityStats: {ex.Message}. Providing scene-based estimates instead." },
                    { "estimatedData", GetEstimatedRenderingStats() }
                };
            }
        }

        private static Dictionary<string, object> GetEstimatedRenderingStats()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            int renderers = 0;
            int triangles = 0;
            int vertices = 0;
            var matSet = new HashSet<Material>();

            foreach (var root in rootObjects)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    renderers++;
                    foreach (var m in r.sharedMaterials)
                        if (m != null) matSet.Add(m);
                }
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh;
                    if (mesh != null)
                    {
                        triangles += mesh.triangles.Length / 3;
                        vertices += mesh.vertexCount;
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "activeRenderers", renderers },
                { "uniqueMaterials", matSet.Count },
                { "estimatedTriangles", triangles },
                { "estimatedVertices", vertices }
            };
        }

        private static object SuggestOptimizations(Dictionary<string, object> p)
        {
            var suggestions = new List<object>();
            int priority = 1;

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            // --- Hierarchy analysis ---
            int totalObjects = 0;
            int maxDepth = 0;
            foreach (var root in rootObjects)
            {
                MeasureDepth(root.transform, 0, ref totalObjects, ref maxDepth);
            }

            if (maxDepth > 10)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Hierarchy" },
                    { "issue", $"Deep hierarchy detected (depth: {maxDepth})" },
                    { "suggestion", "Flatten hierarchy where possible. Deep hierarchies cause expensive Transform updates." }
                });
            }

            if (totalObjects > 5000)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Hierarchy" },
                    { "issue", $"High GameObject count ({totalObjects})" },
                    { "suggestion", "Consider combining static meshes, using instancing, or implementing object pooling." }
                });
            }

            // --- Renderer / Draw call analysis ---
            int nonStaticRenderers = 0;
            int totalRenderers = 0;
            int transparentRenderers = 0;
            var matSet = new HashSet<Material>();

            foreach (var root in rootObjects)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    totalRenderers++;

                    bool isStatic = GameObjectUtility.AreStaticEditorFlagsSet(
                        renderer.gameObject, StaticEditorFlags.BatchingStatic);
                    if (!isStatic) nonStaticRenderers++;

                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) continue;
                        matSet.Add(mat);
                        if (mat.renderQueue >= 3000)
                            transparentRenderers++;
                    }
                }
            }

            if (nonStaticRenderers > 50)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Batching" },
                    { "issue", $"{nonStaticRenderers} renderers are not marked as Batching Static" },
                    { "suggestion", "Mark non-moving objects as Static to enable static batching and reduce draw calls." }
                });
            }

            if (matSet.Count > 100)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Materials" },
                    { "issue", $"High material count ({matSet.Count} unique materials)" },
                    { "suggestion", "Use material atlasing or shared materials to reduce SetPass calls." }
                });
            }

            // --- Texture analysis (lightweight) ---
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int readWriteTextures = 0;
            int oversizedTextures = 0;
            foreach (var guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.isReadable) readWriteTextures++;
                if (importer.maxTextureSize > 2048) oversizedTextures++;
            }

            if (readWriteTextures > 5)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Textures" },
                    { "issue", $"{readWriteTextures} textures have Read/Write enabled" },
                    { "suggestion", "Disable Read/Write on textures that don't need CPU access to save 50% memory per texture." }
                });
            }

            if (oversizedTextures > 3)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Textures" },
                    { "issue", $"{oversizedTextures} textures exceed 2048px max size" },
                    { "suggestion", "Reduce texture max sizes where visual quality allows. Use 'analyze_textures' for details." }
                });
            }

            // --- Mesh analysis (lightweight) ---
            int readWriteMeshes = 0;
            int highPolyMeshes = 0;
            int noLodObjects = 0;
            foreach (var root in rootObjects)
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    if (mesh.isReadable) readWriteMeshes++;
                    if (mesh.vertexCount > 10000)
                    {
                        highPolyMeshes++;
                        if (mf.GetComponent<LODGroup>() == null &&
                            mf.GetComponentInParent<LODGroup>() == null)
                            noLodObjects++;
                    }
                }
            }

            if (readWriteMeshes > 5)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Meshes" },
                    { "issue", $"{readWriteMeshes} meshes have Read/Write enabled" },
                    { "suggestion", "Disable Read/Write on mesh imports to halve their runtime memory." }
                });
            }

            if (noLodObjects > 3)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "LOD" },
                    { "issue", $"{noLodObjects} high-poly objects (>10K verts) have no LOD Group" },
                    { "suggestion", "Add LOD Groups to complex meshes using 'generate_lod_group' to reduce distant rendering cost." }
                });
            }

            // --- Overdraw ---
            if (transparentRenderers > 20)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Overdraw" },
                    { "issue", $"{transparentRenderers} transparent material instances detected" },
                    { "suggestion", "Minimize transparent objects. Use 'analyze_overdraw' for detailed overlap analysis." }
                });
            }

            // --- Lights ---
            int realtimeLights = 0;
            foreach (var root in rootObjects)
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.enabled && light.lightmapBakeType == LightmapBakeType.Realtime)
                        realtimeLights++;
                }
            }

            if (realtimeLights > 8)
            {
                suggestions.Add(new Dictionary<string, object>
                {
                    { "priority", priority++ },
                    { "category", "Lighting" },
                    { "issue", $"{realtimeLights} real-time lights in scene" },
                    { "suggestion", "Bake static lights using 'bake_lighting'. Use Light Probes for dynamic objects." }
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "sceneName", scene.name },
                { "totalSuggestions", suggestions.Count },
                { "summary", new Dictionary<string, object>
                    {
                        { "gameObjects", totalObjects },
                        { "renderers", totalRenderers },
                        { "uniqueMaterials", matSet.Count },
                        { "maxHierarchyDepth", maxDepth }
                    }
                },
                { "suggestions", suggestions }
            };
        }

        private static void MeasureDepth(Transform node, int depth, ref int totalNodes, ref int maxDepth)
        {
            totalNodes++;
            if (depth > maxDepth) maxDepth = depth;
            foreach (Transform child in node)
                MeasureDepth(child, depth + 1, ref totalNodes, ref maxDepth);
        }

        private static object AnalyzeOverdraw(Dictionary<string, object> p)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var transparentRenderers = new List<Renderer>();
            var uiGraphics = new List<object>();
            var overlaps = new List<object>();

            foreach (var root in rootObjects)
            {
                // Collect transparent renderers
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.renderQueue >= 3000)
                        {
                            transparentRenderers.Add(renderer);
                            break;
                        }
                    }
                }

                // Collect UI elements (Canvas renderers)
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    int childCount = canvas.GetComponentsInChildren<Transform>(true).Length - 1;
                    var canvasRenderers = canvas.GetComponentsInChildren<CanvasRenderer>(true);
                    uiGraphics.Add(new Dictionary<string, object>
                    {
                        { "canvas", GetGameObjectPath(canvas.gameObject) },
                        { "renderMode", canvas.renderMode.ToString() },
                        { "childElements", childCount },
                        { "canvasRenderers", canvasRenderers.Length },
                        { "overrideSorting", canvas.overrideSorting }
                    });
                }
            }

            // Check for overlapping transparent renderer bounds
            for (int i = 0; i < transparentRenderers.Count; i++)
            {
                for (int j = i + 1; j < transparentRenderers.Count; j++)
                {
                    if (transparentRenderers[i].bounds.Intersects(transparentRenderers[j].bounds))
                    {
                        overlaps.Add(new Dictionary<string, object>
                        {
                            { "objectA", GetGameObjectPath(transparentRenderers[i].gameObject) },
                            { "objectB", GetGameObjectPath(transparentRenderers[j].gameObject) }
                        });

                        // Cap overlap results
                        if (overlaps.Count >= 50)
                            break;
                    }
                }
                if (overlaps.Count >= 50)
                    break;
            }

            var suggestions = new List<string>();
            if (transparentRenderers.Count > 10)
                suggestions.Add("Reduce the number of transparent objects. Consider opaque alternatives with alpha-tested cutout shaders.");
            if (overlaps.Count > 5)
                suggestions.Add("Multiple transparent objects overlap, causing overdraw. Reduce layering or combine into fewer draw calls.");
            if (uiGraphics.Any(u => u is Dictionary<string, object> d &&
                d.ContainsKey("canvasRenderers") && (int)d["canvasRenderers"] > 50))
                suggestions.Add("UI Canvas has many renderers. Split static and dynamic UI into separate Canvases to reduce rebuild cost.");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "transparentRendererCount", transparentRenderers.Count },
                { "overlappingPairs", overlaps.Count },
                { "overlaps", overlaps },
                { "uiCanvases", uiGraphics },
                { "capped", overlaps.Count >= 50 },
                { "suggestions", suggestions }
            };
        }
    }
}
