using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityMcpPro
{
    [InitializeOnLoad]
    public class McpPlugin : EditorWindow
    {
        private static McpPlugin _instance;
        private static WebSocketServer _wsServer;
        private static CommandRouter _router;
        private static bool _initialized;

        static McpPlugin()
        {
            // Use update callback as primary initialization trigger
            // This is more reliable than delayCall across domain reloads
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        [MenuItem("Window/Unity MCP Pro")]
        public static void ShowWindow()
        {
            _instance = GetWindow<McpPlugin>("Unity MCP Pro");
        }

        private static void OnEditorUpdate()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _router = new CommandRouter();
            RegisterCommands();

            _wsServer = new WebSocketServer(_router);
            _wsServer.Start();

            Debug.Log("[MCP] Unity MCP Pro plugin initialized");
        }

        private static void RegisterCommands()
        {
            // MVP (26 tools)
            ProjectCommands.Register(_router);
            SceneCommands.Register(_router);
            GameObjectCommands.Register(_router);
            ScriptCommands.Register(_router);
            EditorCommands.Register(_router);

            // Tier 1 (29 tools)
            PrefabCommands.Register(_router);
            MaterialCommands.Register(_router);
            PhysicsCommands.Register(_router);
            LightingCommands.Register(_router);
#if HAS_UGUI
            UICommands.Register(_router);
#endif

            // Tier 2 (22 tools)
            AnimationCommands.Register(_router);
            BuildCommands.Register(_router);
            BatchCommands.Register(_router);
            AudioCommands.Register(_router);

            // Tier 3 (15 tools)
            AnalysisCommands.Register(_router);
            NavigationCommands.Register(_router);
            ParticleCommands.Register(_router);
            PackageCommands.Register(_router);
            TerrainCommands.Register(_router);

            // Debug (5 tools)
            DebugCommands.Register(_router);

            // Input Simulation (4 + 4 tools)
            InputCommands.Register(_router);

            // Screenshot & Visual (4 tools)
            ScreenshotCommands.Register(_router);

            // Runtime Extended (7 tools)
            RuntimeCommands.Register(_router);

            // Testing & QA (6 tools)
            TestingCommands.Register(_router);

            // 2D Tools (6 tools)
            TwoDCommands.Register(_router);

            // Controller Tools (4 tools)
            ControllerCommands.Register(_router);

            // Animation Extended (5 tools)
            AnimationExtendedCommands.Register(_router);

            // Environment (6 tools)
            EnvironmentCommands.Register(_router);

            // Timeline (5 tools)
#if HAS_TIMELINE
            TimelineCommands.Register(_router);
#endif

            // Optimization (7 tools)
            OptimizationCommands.Register(_router);

            // Camera / Cinemachine (6 tools)
            CameraCommands.Register(_router);

            // Post-Processing (5 tools)
            PostProcessCommands.Register(_router);

            // AI Tools (4 tools)
            AICommands.Register(_router);

            // Game System Tools (5 tools)
            GameSystemCommands.Register(_router);

            // Shader Graph (5 tools)
            ShaderGraphCommands.Register(_router);

            // Visual Scripting (5 tools)
            VisualScriptCommands.Register(_router);

            // Profiler (4 tools)
            ProfilerCommands.Register(_router);

            // Asset Import (5 tools)
            ImportCommands.Register(_router);

            // Spline (5 tools)
            SplineCommands.Register(_router);

            // Multi-Scene (4 tools)
            MultiSceneCommands.Register(_router);

            // Scene View Camera (4 tools)
            SceneViewCommands.Register(_router);

            // Automated Playthrough (4 tools)
            PlaythroughCommands.Register(_router);

            // Benchmark (4 tools)
            BenchmarkCommands.Register(_router);

            // Live Property Watch (3 tools)
            WatchCommands.Register(_router);

            // Addressables (5 tools)
            AddressableCommands.Register(_router);

            // Localization (4 tools)
            LocalizationCommands.Register(_router);

            // Custom Editor Generation (4 tools)
            CustomEditorCommands.Register(_router);

            // Undo History (3 tools)
            UndoCommands.Register(_router);

            // Animation Rigging (4 tools)
            RiggingCommands.Register(_router);

            // ECS/DOTS (4 tools)
            ECSCommands.Register(_router);

            // Netcode (4 tools)
            NetcodeCommands.Register(_router);

            // XR/VR (4 tools)
            XRCommands.Register(_router);
        }

        private static void OnBeforeAssemblyReload()
        {
            _wsServer?.Stop();
            _wsServer = null;
            _router = null;
            _initialized = false;
        }

        private void OnDestroy()
        {
            // Window closed, but keep server running
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity MCP Pro", EditorStyles.boldLabel);
            GUILayout.Space(10);

            bool connected = _wsServer != null && _wsServer.IsConnected;
            int count = _wsServer?.ConnectionCount ?? 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Status:");
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = connected ? Color.green : Color.yellow;
            string statusText = connected
                ? $"Connected ({count} session{(count > 1 ? "s" : "")})"
                : "Waiting for MCP server...";
            GUILayout.Label(statusText, style);
            EditorGUILayout.EndHorizontal();

            if (_wsServer != null && connected)
            {
                var ports = _wsServer.ConnectedPorts.ToArray();
                EditorGUILayout.LabelField("Ports", string.Join(", ", ports));
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Restart Connection"))
            {
                _wsServer?.Stop();
                _wsServer = null;
                _router = null;
                _initialized = false;
                Initialize();
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
