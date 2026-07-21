using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    /// <summary>
    /// Shader Graph manipulation tools. Works by directly editing the .shadergraph JSON files,
    /// which avoids hard dependencies on the Shader Graph package.
    /// </summary>
    public class ShaderGraphCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_shader_graph", CreateShaderGraph);
            router.Register("add_shader_node", AddShaderNode);
            router.Register("connect_shader_nodes", ConnectShaderNodes);
            router.Register("set_shader_property", SetShaderProperty);
            router.Register("get_shader_graph_info", GetShaderGraphInfo);
        }

        // ───────────────────────── helpers ─────────────────────────

        private static string GenerateGuid()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 32);
        }

        private static Vector2 ParsePosition(string pos)
        {
            if (string.IsNullOrEmpty(pos)) return Vector2.zero;
            var parts = pos.Split(',');
            float x = parts.Length > 0 ? float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f;
            float y = parts.Length > 1 ? float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Reads and parses the .shadergraph JSON. The file is a JSON object with
        /// "m_SerializedNodes", "m_SerializedEdges", "m_SerializedProperties", etc.
        /// We use a lightweight MiniJSON-style approach (Dictionary parsing via Unity's JsonUtility is not
        /// suitable for arbitrary JSON), so we work at the string/text level for maximum compatibility.
        /// </summary>
        private static string ReadGraphFile(string path)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            if (!File.Exists(fullPath))
                throw new ArgumentException($"Shader Graph file not found: {path}");
            return File.ReadAllText(fullPath);
        }

        private static void WriteGraphFile(string path, string content)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            File.WriteAllText(fullPath, content);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                string fullDir = Path.Combine(Application.dataPath.Replace("/Assets", ""), dir);
                if (!Directory.Exists(fullDir))
                    Directory.CreateDirectory(fullDir);
            }
        }

        // ───────────────────── node type GUIDs ─────────────────────
        // These map user-friendly node names to Shader Graph internal type identifiers.

        private static readonly Dictionary<string, string> NodeTypeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Color",           "UnityEditor.ShaderGraph.ColorNode" },
            { "Texture2D",       "UnityEditor.ShaderGraph.Texture2DAssetNode" },
            { "UV",              "UnityEditor.ShaderGraph.UVNode" },
            { "Multiply",        "UnityEditor.ShaderGraph.MultiplyNode" },
            { "Add",             "UnityEditor.ShaderGraph.AddNode" },
            { "Lerp",            "UnityEditor.ShaderGraph.LerpNode" },
            { "Normal",          "UnityEditor.ShaderGraph.NormalVectorNode" },
            { "Fresnel",         "UnityEditor.ShaderGraph.FresnelNode" },
            { "Time",            "UnityEditor.ShaderGraph.TimeNode" },
            { "SampleTexture2D", "UnityEditor.ShaderGraph.SampleTexture2DNode" },
            { "Split",           "UnityEditor.ShaderGraph.SplitNode" },
            { "Combine",         "UnityEditor.ShaderGraph.CombineNode" },
            { "OneMinus",        "UnityEditor.ShaderGraph.OneMinusNode" },
            { "Saturate",        "UnityEditor.ShaderGraph.SaturateNode" },
            { "Power",           "UnityEditor.ShaderGraph.PowerNode" },
            { "Step",            "UnityEditor.ShaderGraph.StepNode" },
            { "SmoothStep",      "UnityEditor.ShaderGraph.SmoothstepNode" },
        };

        // ───────────────────── minimal graph templates ─────────────────────

        private static string GetLitTemplate()
        {
            return @"{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """ + GenerateGuid() + @""",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {
        ""m_Position"": { ""x"": 0.0, ""y"": 0.0 },
        ""m_Blocks"": []
    },
    ""m_FragmentContext"": {
        ""m_Position"": { ""x"": 200.0, ""y"": 0.0 },
        ""m_Blocks"": []
    },
    ""m_PreviewData"": {
        ""serializedMesh"": { ""m_SerializedMesh"": """", ""m_Guid"": """" }
    },
    ""m_Path"": ""Shader Graphs"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {
        ""m_Id"": """ + GenerateGuid() + @"""
    },
    ""m_ActiveTargets"": [
        {
            ""m_Id"": ""target_lit"",
            ""m_Type"": ""UnityEditor.Rendering.Universal.UniversalLitSubTarget""
        }
    ]
}";
        }

        private static string GetUnlitTemplate()
        {
            string template = GetLitTemplate();
            return template
                .Replace("UniversalLitSubTarget", "UniversalUnlitSubTarget")
                .Replace("\"target_lit\"", "\"target_unlit\"");
        }

        private static string GetSpriteTemplate()
        {
            string template = GetLitTemplate();
            return template
                .Replace("UniversalLitSubTarget", "UniversalSpriteLitSubTarget")
                .Replace("\"target_lit\"", "\"target_sprite\"");
        }

        // ───────────────────── JSON array helpers ─────────────────────
        // Lightweight helpers to insert entries into JSON arrays without a full JSON parser.

        private static string InsertIntoJsonArray(string json, string arrayKey, string newEntry)
        {
            // Find the array by key, e.g. "m_Nodes": [...]
            string pattern = $"\"{arrayKey}\"\\s*:\\s*\\[";
            var match = Regex.Match(json, pattern);
            if (!match.Success)
                throw new InvalidOperationException($"Could not find array '{arrayKey}' in shader graph JSON");

            int bracketPos = json.IndexOf('[', match.Index + match.Length - 1);

            // Find if array is empty or has content
            int afterBracket = bracketPos + 1;
            string afterContent = json.Substring(afterBracket).TrimStart();
            if (afterContent.StartsWith("]"))
            {
                // Empty array — insert directly
                return json.Substring(0, afterBracket) + "\n        " + newEntry + "\n    " + json.Substring(afterBracket);
            }
            else
            {
                // Non-empty array — add comma after last entry before closing bracket
                // Find matching closing bracket
                int depth = 1;
                int i = afterBracket;
                int lastEntryEnd = afterBracket;
                while (i < json.Length && depth > 0)
                {
                    if (json[i] == '[' || json[i] == '{') depth++;
                    else if (json[i] == ']' || json[i] == '}')
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                    if (depth == 1 && json[i] == ',' || (depth == 1 && json[i] == '}'))
                        lastEntryEnd = i + 1;
                    i++;
                }
                // Insert before closing bracket
                int closingBracket = i;
                string before = json.Substring(0, closingBracket).TrimEnd();
                if (!before.EndsWith(","))
                    before += ",";
                return before + "\n        " + newEntry + "\n    " + json.Substring(closingBracket);
            }
        }

        // ───────────────────── command handlers ─────────────────────

        private static object CreateShaderGraph(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_shader_graph");
            string path = GetStringParam(p, "path");
            string type = GetStringParam(p, "type", "Lit");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            if (!path.EndsWith(".shadergraph"))
                path += ".shadergraph";

            EnsureDirectoryExists(path);

            string template;
            switch (type.ToLowerInvariant())
            {
                case "unlit":
                    template = GetUnlitTemplate();
                    break;
                case "sprite":
                    template = GetSpriteTemplate();
                    break;
                case "lit":
                default:
                    template = GetLitTemplate();
                    break;
            }

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            File.WriteAllText(fullPath, template);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "type", type }
            };
        }

        private static object AddShaderNode(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_shader_node");
            string graphPath = GetStringParam(p, "graph_path");
            string nodeType = GetStringParam(p, "node_type");
            string nodeId = GetStringParam(p, "node_id");
            string posStr = GetStringParam(p, "position", "0,0");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(nodeType))
                throw new ArgumentException("node_type is required");

            if (!NodeTypeIds.TryGetValue(nodeType, out string fullTypeName))
                throw new ArgumentException($"Unknown node type: {nodeType}. Supported: {string.Join(", ", NodeTypeIds.Keys)}");

            var pos = ParsePosition(posStr);
            if (string.IsNullOrEmpty(nodeId))
                nodeId = nodeType.ToLower() + "_" + GenerateGuid().Substring(0, 8);

            string json = ReadGraphFile(graphPath);

            string nodeEntry = "{"
                + $"\"m_Id\": \"{nodeId}\","
                + $"\"m_Type\": \"{fullTypeName}\","
                + $"\"m_Position\": {{\"x\": {pos.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pos.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"
                + "}";

            json = InsertIntoJsonArray(json, "m_Nodes", nodeEntry);
            WriteGraphFile(graphPath, json);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "node_id", nodeId },
                { "node_type", nodeType },
                { "position", posStr }
            };
        }

        private static object ConnectShaderNodes(Dictionary<string, object> p)
        {
            ThrowIfPlaying("connect_shader_nodes");
            string graphPath = GetStringParam(p, "graph_path");
            string fromNode = GetStringParam(p, "from_node");
            string fromPort = GetStringParam(p, "from_port");
            string toNode = GetStringParam(p, "to_node");
            string toPort = GetStringParam(p, "to_port");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(fromNode))
                throw new ArgumentException("from_node is required");
            if (string.IsNullOrEmpty(fromPort))
                throw new ArgumentException("from_port is required");
            if (string.IsNullOrEmpty(toNode))
                throw new ArgumentException("to_node is required");
            if (string.IsNullOrEmpty(toPort))
                throw new ArgumentException("to_port is required");

            string json = ReadGraphFile(graphPath);

            string edgeId = "edge_" + GenerateGuid().Substring(0, 8);
            string edgeEntry = "{"
                + $"\"m_Id\": \"{edgeId}\","
                + $"\"m_OutputSlot\": {{\"m_Node\": \"{fromNode}\", \"m_Port\": \"{fromPort}\"}},"
                + $"\"m_InputSlot\": {{\"m_Node\": \"{toNode}\", \"m_Port\": \"{toPort}\"}}"
                + "}";

            json = InsertIntoJsonArray(json, "m_Edges", edgeEntry);
            WriteGraphFile(graphPath, json);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "edge_id", edgeId },
                { "from", $"{fromNode}.{fromPort}" },
                { "to", $"{toNode}.{toPort}" }
            };
        }

        private static object SetShaderProperty(Dictionary<string, object> p)
        {
            ThrowIfPlaying("set_shader_property");
            string graphPath = GetStringParam(p, "graph_path");
            string propertyName = GetStringParam(p, "property_name");
            string propertyType = GetStringParam(p, "property_type");
            string defaultValue = GetStringParam(p, "default_value");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("property_name is required");
            if (string.IsNullOrEmpty(propertyType))
                throw new ArgumentException("property_type is required");

            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Color",     "UnityEditor.ShaderGraph.Internal.ColorShaderProperty" },
                { "Float",     "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty" },
                { "Vector2",   "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty" },
                { "Vector3",   "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty" },
                { "Vector4",   "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty" },
                { "Texture2D", "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty" },
                { "Boolean",   "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty" },
            };

            if (!typeMap.TryGetValue(propertyType, out string fullType))
                throw new ArgumentException($"Unknown property type: {propertyType}. Supported: {string.Join(", ", typeMap.Keys)}");

            string json = ReadGraphFile(graphPath);

            string propId = GenerateGuid().Substring(0, 8);
            string referenceName = "_" + Regex.Replace(propertyName, @"[^a-zA-Z0-9]", "");

            string defaultValueJson = "{}";
            if (!string.IsNullOrEmpty(defaultValue))
            {
                defaultValueJson = $"{{\"m_DefaultValue\": \"{defaultValue}\"}}";
            }

            string propEntry = "{"
                + $"\"m_Id\": \"prop_{propId}\","
                + $"\"m_Type\": \"{fullType}\","
                + $"\"m_DisplayName\": \"{propertyName}\","
                + $"\"m_ReferenceName\": \"{referenceName}\","
                + $"\"m_Value\": {defaultValueJson}"
                + "}";

            json = InsertIntoJsonArray(json, "m_Properties", propEntry);
            WriteGraphFile(graphPath, json);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "property_name", propertyName },
                { "property_type", propertyType },
                { "reference_name", referenceName }
            };
        }

        private static object GetShaderGraphInfo(Dictionary<string, object> p)
        {
            string graphPath = GetStringParam(p, "graph_path");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");

            string json = ReadGraphFile(graphPath);

            // Extract counts and basic info by scanning the JSON
            var nodes = new List<object>();
            var edges = new List<object>();
            var properties = new List<object>();

            // Count nodes
            int nodeCount = CountJsonArrayEntries(json, "m_Nodes");
            int edgeCount = CountJsonArrayEntries(json, "m_Edges");
            int propertyCount = CountJsonArrayEntries(json, "m_Properties");

            // Extract node IDs and types
            var nodeMatches = Regex.Matches(json, @"""m_Id""\s*:\s*""([^""]+)""[^}]*?""m_Type""\s*:\s*""([^""]+)""");
            foreach (Match m in nodeMatches)
            {
                if (m.Groups[2].Value.Contains("Node") || m.Groups[2].Value.Contains("ShaderGraph"))
                {
                    nodes.Add(new Dictionary<string, object>
                    {
                        { "id", m.Groups[1].Value },
                        { "type", m.Groups[2].Value.Split('.').Last() }
                    });
                }
            }

            // Extract edge info
            var edgeMatches = Regex.Matches(json,
                @"""m_Id""\s*:\s*""(edge_[^""]+)"".*?""m_OutputSlot"".*?""m_Node""\s*:\s*""([^""]+)"".*?""m_Port""\s*:\s*""([^""]+)"".*?""m_InputSlot"".*?""m_Node""\s*:\s*""([^""]+)"".*?""m_Port""\s*:\s*""([^""]+)""",
                RegexOptions.Singleline);
            foreach (Match m in edgeMatches)
            {
                edges.Add(new Dictionary<string, object>
                {
                    { "id", m.Groups[1].Value },
                    { "from_node", m.Groups[2].Value },
                    { "from_port", m.Groups[3].Value },
                    { "to_node", m.Groups[4].Value },
                    { "to_port", m.Groups[5].Value }
                });
            }

            // Extract property info
            var propMatches = Regex.Matches(json,
                @"""m_DisplayName""\s*:\s*""([^""]+)"".*?""m_ReferenceName""\s*:\s*""([^""]+)""",
                RegexOptions.Singleline);
            foreach (Match m in propMatches)
            {
                properties.Add(new Dictionary<string, object>
                {
                    { "display_name", m.Groups[1].Value },
                    { "reference_name", m.Groups[2].Value }
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "node_count", nodeCount },
                { "edge_count", edgeCount },
                { "property_count", propertyCount },
                { "nodes", nodes },
                { "edges", edges },
                { "properties", properties }
            };
        }

        private static int CountJsonArrayEntries(string json, string arrayKey)
        {
            string pattern = $"\"{arrayKey}\"\\s*:\\s*\\[";
            var match = Regex.Match(json, pattern);
            if (!match.Success) return 0;

            int bracketPos = json.IndexOf('[', match.Index);
            // Find closing bracket and count entries by counting top-level commas + 1
            int depth = 0;
            int count = 0;
            bool hasContent = false;

            for (int i = bracketPos; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (hasContent) count++;
                        break;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    count++;
                    hasContent = true;
                }
                else if (!char.IsWhiteSpace(c) && depth == 1 && !hasContent)
                {
                    hasContent = true;
                }
            }
            return count;
        }
    }
}
