using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UnityMcpPro
{
    public class MCPServerSetup : EditorWindow
    {
        private string _nodeVersion;
        private bool _nodeAvailable;
        private bool _npmInstalled;
        private string _serverPath;
        private Vector2 _scrollPos;
        private string _statusMessage;
        private MessageType _statusType;
        private int _selectedIDE;
        private static readonly string[] IDEOptions = {
            "Claude Code",
            "Claude Desktop",
            "Cursor",
            "VS Code (GitHub Copilot)",
            "Windsurf"
        };

        [MenuItem("Window/Unity MCP Pro/Server Setup")]
        public static void ShowWindow()
        {
            GetWindow<MCPServerSetup>("MCP Server Setup");
        }

        private void OnEnable()
        {
            _serverPath = FindServerPath();
            CheckNodeInstallation();
            CheckNpmInstalled();
        }

        private string FindServerPath()
        {
            // Find the Server~ folder relative to this package
            var guids = AssetDatabase.FindAssets("t:asmdef com.unity-mcp-pro.editor");
            if (guids.Length > 0)
            {
                var asmdefPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var editorDir = Path.GetDirectoryName(asmdefPath);
                var packageDir = Path.GetDirectoryName(editorDir);
                var serverDir = Path.Combine(packageDir, "Server~");
                if (Directory.Exists(serverDir))
                    return Path.GetFullPath(serverDir);
            }

            // Fallback: search in Packages
            var packagesServer = Path.GetFullPath("Packages/com.unity-mcp-pro/Server~");
            if (Directory.Exists(packagesServer))
                return packagesServer;

            // Fallback: search in Assets
            var assetsDir = Path.GetFullPath("Assets");
            foreach (var dir in Directory.GetDirectories(assetsDir, "Server~", SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(dir, "package.json")))
                    return dir;
            }

            return null;
        }

        private void CheckNodeInstallation()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                _nodeVersion = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                _nodeAvailable = process.ExitCode == 0 && !string.IsNullOrEmpty(_nodeVersion);
            }
            catch
            {
                _nodeAvailable = false;
                _nodeVersion = null;
            }
        }

        private void CheckNpmInstalled()
        {
            if (string.IsNullOrEmpty(_serverPath)) return;
            _npmInstalled = Directory.Exists(Path.Combine(_serverPath, "node_modules"));
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("Unity MCP Pro — Server Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            DrawPrerequisites();
            GUILayout.Space(10);
            DrawServerInstall();
            GUILayout.Space(10);
            DrawIDEConfig();
            GUILayout.Space(10);
            DrawStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrerequisites()
        {
            GUILayout.Label("Prerequisites", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawStatusIcon(_nodeAvailable);
            if (_nodeAvailable)
                GUILayout.Label($"Node.js: {_nodeVersion}");
            else
                GUILayout.Label("Node.js: Not found (requires v18+)");
            if (GUILayout.Button("Recheck", GUILayout.Width(70)))
                CheckNodeInstallation();
            EditorGUILayout.EndHorizontal();

            if (!_nodeAvailable)
            {
                EditorGUILayout.HelpBox(
                    "Node.js 18+ is required. Download from https://nodejs.org/",
                    MessageType.Warning);
                if (GUILayout.Button("Open Node.js Download Page"))
                    Application.OpenURL("https://nodejs.org/");
            }
        }

        private void DrawServerInstall()
        {
            GUILayout.Label("MCP Server", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_serverPath))
            {
                EditorGUILayout.HelpBox(
                    "Server~ folder not found. This package may not include the MCP server.\n" +
                    "If you installed via Git URL, you need to set up the server separately.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawStatusIcon(_npmInstalled);
            GUILayout.Label(_npmInstalled ? "Dependencies installed" : "Dependencies not installed");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Server Path", _serverPath, EditorStyles.wordWrappedLabel);

            if (!_npmInstalled)
            {
                EditorGUI.BeginDisabledGroup(!_nodeAvailable);
                if (GUILayout.Button("Install Dependencies (npm install)"))
                    RunNpmInstall();
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                if (GUILayout.Button("Reinstall Dependencies"))
                    RunNpmInstall();
            }
        }

        private void DrawIDEConfig()
        {
            GUILayout.Label("IDE Configuration", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_serverPath) || !_npmInstalled)
            {
                EditorGUILayout.HelpBox(
                    "Install server dependencies first before configuring your IDE.",
                    MessageType.Info);
                return;
            }

            _selectedIDE = EditorGUILayout.Popup("Target IDE", _selectedIDE, IDEOptions);

            var indexJsPath = Path.Combine(_serverPath, "build", "index.js").Replace("\\", "/");

            GUILayout.Space(5);

            switch (_selectedIDE)
            {
                case 0: // Claude Code
                    DrawClaudeCodeConfig(indexJsPath);
                    break;
                case 1: // Claude Desktop
                    DrawJsonConfig(indexJsPath, "claude_desktop_config.json",
                        GetClaudeDesktopConfigPath(), "mcpServers");
                    break;
                case 2: // Cursor
                    DrawJsonConfig(indexJsPath, ".cursor/mcp.json",
                        null, "mcpServers");
                    break;
                case 3: // VS Code
                    DrawJsonConfig(indexJsPath, ".vscode/mcp.json",
                        null, "servers");
                    break;
                case 4: // Windsurf
                    DrawJsonConfig(indexJsPath, "mcp_config.json",
                        GetWindsurfConfigPath(), "mcpServers");
                    break;
            }
        }

        private void DrawClaudeCodeConfig(string indexJsPath)
        {
            var command = $"claude mcp add unity -- node \"{indexJsPath}\"";

            EditorGUILayout.HelpBox(
                "Run the following command in your terminal:",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(command, EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = command;
                SetStatus("Command copied to clipboard!", MessageType.Info);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawJsonConfig(string indexJsPath, string fileName,
            string knownPath, string serversKey)
        {
            var json = GenerateConfigJson(indexJsPath, serversKey);

            EditorGUILayout.HelpBox(
                $"Add the following to your {fileName}:",
                MessageType.Info);

            EditorGUILayout.TextArea(json, GUILayout.MinHeight(80));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy JSON"))
            {
                EditorGUIUtility.systemCopyBuffer = json;
                SetStatus("JSON copied to clipboard!", MessageType.Info);
            }
            if (knownPath != null && GUILayout.Button("Open Config File"))
            {
                if (File.Exists(knownPath))
                    EditorUtility.RevealInFinder(knownPath);
                else
                    EditorUtility.RevealInFinder(Path.GetDirectoryName(knownPath));
            }
            EditorGUILayout.EndHorizontal();
        }

        private string GenerateConfigJson(string indexJsPath, string serversKey)
        {
            return "{\n" +
                   $"  \"{serversKey}\": {{\n" +
                   "    \"unity\": {\n" +
                   "      \"command\": \"node\",\n" +
                   $"      \"args\": [\"{EscapeJsonString(indexJsPath)}\"]\n" +
                   "    }\n" +
                   "  }\n" +
                   "}";
        }

        private string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private string GetClaudeDesktopConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Claude", "claude_desktop_config.json");
        }

        private string GetWindsurfConfigPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".codeium", "windsurf", "mcp_config.json");
        }

        private void RunNpmInstall()
        {
            SetStatus("Installing dependencies...", MessageType.Info);

            try
            {
                var isWindows = Application.platform == RuntimePlatform.WindowsEditor;
                var psi = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/bash",
                    Arguments = isWindows
                        ? "/c npm install --production"
                        : "-c \"npm install --production\"",
                    WorkingDirectory = _serverPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    _npmInstalled = true;
                    SetStatus("Dependencies installed successfully!", MessageType.Info);
                }
                else
                {
                    SetStatus($"npm install failed:\n{error}", MessageType.Error);
                }
            }
            catch (Exception e)
            {
                SetStatus($"Failed to run npm install: {e.Message}", MessageType.Error);
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
                if (GUILayout.Button("Clear"))
                {
                    _statusMessage = null;
                }
            }
        }

        private void DrawStatusIcon(bool ok)
        {
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = ok ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f);
            GUILayout.Label(ok ? "●" : "○", style, GUILayout.Width(16));
        }
    }
}
