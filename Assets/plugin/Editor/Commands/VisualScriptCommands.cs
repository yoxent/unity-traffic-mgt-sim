using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    /// <summary>
    /// Visual Scripting (formerly Bolt) tools. Uses reflection to avoid hard dependency
    /// on the Unity.VisualScripting package.
    /// </summary>
    public class VisualScriptCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_visual_script", CreateVisualScript);
            router.Register("add_script_node", AddScriptNode);
            router.Register("connect_script_nodes", ConnectScriptNodes);
            router.Register("add_script_variable", AddScriptVariable);
            router.Register("get_visual_script_info", GetVisualScriptInfo);
        }

        // ───────────────────── reflection helpers ─────────────────────

        private static Assembly _vsAssembly;
        private static bool _vsChecked;

        private static Assembly GetVSAssembly()
        {
            if (_vsChecked) return _vsAssembly;
            _vsChecked = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Unity.VisualScripting.Flow" ||
                    asm.GetName().Name == "Unity.VisualScripting.Core")
                {
                    _vsAssembly = asm;
                    return _vsAssembly;
                }
            }
            return null;
        }

        private static Assembly GetVSCoreAssembly()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Unity.VisualScripting.Core")
                    return asm;
            }
            return null;
        }

        private static Assembly GetVSFlowAssembly()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Unity.VisualScripting.Flow")
                    return asm;
            }
            return null;
        }

        private static void EnsureVSAvailable()
        {
            if (GetVSAssembly() == null)
                throw new InvalidOperationException(
                    "Unity Visual Scripting package is not installed. " +
                    "Install it via Package Manager: com.unity.visualscripting");
        }

        private static Type FindVSType(string typeName)
        {
            // Search across all VS assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name;
                if (!asmName.StartsWith("Unity.VisualScripting")) continue;

                var type = asm.GetType(typeName, false);
                if (type != null) return type;

                // Also try with namespace prefix
                type = asm.GetType("Unity.VisualScripting." + typeName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static Vector2 ParsePosition(string pos)
        {
            if (string.IsNullOrEmpty(pos)) return Vector2.zero;
            var parts = pos.Split(',');
            float x = parts.Length > 0 ? float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f;
            float y = parts.Length > 1 ? float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f;
            return new Vector2(x, y);
        }

        // ───────────────────── node type mapping ─────────────────────

        private static readonly Dictionary<string, string> NodeTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Start",               "Unity.VisualScripting.Start" },
            { "Update",              "Unity.VisualScripting.Update" },
            { "OnTriggerEnter",      "Unity.VisualScripting.OnTriggerEnter" },
            { "GetVariable",         "Unity.VisualScripting.GetVariable" },
            { "SetVariable",         "Unity.VisualScripting.SetVariable" },
            { "If",                  "Unity.VisualScripting.If" },
            { "ForLoop",             "Unity.VisualScripting.For" },
            { "Debug.Log",           "Unity.VisualScripting.InvokeMember" },
            { "Instantiate",         "Unity.VisualScripting.InvokeMember" },
            { "Destroy",             "Unity.VisualScripting.InvokeMember" },
            { "GetComponent",        "Unity.VisualScripting.GetComponent" },
            { "Transform.Translate", "Unity.VisualScripting.InvokeMember" },
            { "Input.GetKey",        "Unity.VisualScripting.InvokeMember" },
            { "Rigidbody.AddForce",  "Unity.VisualScripting.InvokeMember" },
            { "Timer",               "Unity.VisualScripting.Timer" },
            { "Sequence",            "Unity.VisualScripting.Sequence" },
        };

        // InvokeMember nodes need specific target type and method name
        private static readonly Dictionary<string, (string targetType, string methodName)> InvokeMemberMap =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "Debug.Log",           ("UnityEngine.Debug",      "Log") },
            { "Instantiate",         ("UnityEngine.Object",     "Instantiate") },
            { "Destroy",             ("UnityEngine.Object",     "Destroy") },
            { "Transform.Translate", ("UnityEngine.Transform",  "Translate") },
            { "Input.GetKey",        ("UnityEngine.Input",      "GetKey") },
            { "Rigidbody.AddForce",  ("UnityEngine.Rigidbody",  "AddForce") },
        };

        // ───────────────────── command handlers ─────────────────────

        private static object CreateVisualScript(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_visual_script");
            EnsureVSAvailable();

            string path = GetStringParam(p, "path");
            string type = GetStringParam(p, "type", "Script");
            string target = GetStringParam(p, "target");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            if (!path.EndsWith(".asset"))
                path += ".asset";

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                string fullDir = System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), dir);
                if (!System.IO.Directory.Exists(fullDir))
                    System.IO.Directory.CreateDirectory(fullDir);
            }

            bool isState = type.Equals("State", StringComparison.OrdinalIgnoreCase);

            // Create the graph asset via reflection
            Type assetType;
            if (isState)
            {
                assetType = FindVSType("StateGraphAsset");
            }
            else
            {
                assetType = FindVSType("ScriptGraphAsset");
            }

            if (assetType == null)
                throw new InvalidOperationException($"Could not find Visual Scripting type for '{type}' graph. Ensure the package is properly installed.");

            var asset = ScriptableObject.CreateInstance(assetType);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            string machineTarget = null;

            // Attach to GameObject if specified
            if (!string.IsNullOrEmpty(target))
            {
                var go = FindGameObject(target);
                Type machineType;
                if (isState)
                {
                    machineType = FindVSType("StateMachine");
                }
                else
                {
                    machineType = FindVSType("ScriptMachine");
                }

                if (machineType != null)
                {
                    var machine = Undo.AddComponent(go, machineType);
                    // Set the graph asset on the machine
                    var graphProp = machineType.GetProperty("graph") ?? machineType.GetProperty("nest")?.PropertyType.GetProperty("graph");
                    var nestProp = machineType.GetProperty("nest");
                    if (nestProp != null)
                    {
                        var nest = nestProp.GetValue(machine);
                        if (nest != null)
                        {
                            var sourceProp = nest.GetType().GetProperty("source");
                            // Set source to Macro (external asset)
                            if (sourceProp != null)
                            {
                                var graphSourceType = FindVSType("GraphSource");
                                if (graphSourceType != null && graphSourceType.IsEnum)
                                {
                                    var macroValue = Enum.Parse(graphSourceType, "Macro");
                                    sourceProp.SetValue(nest, macroValue);
                                }
                            }
                            var macroProp = nest.GetType().GetProperty("macro");
                            if (macroProp != null)
                            {
                                macroProp.SetValue(nest, asset);
                            }
                        }
                    }
                    EditorUtility.SetDirty(machine);
                    machineTarget = GetGameObjectPath(go);
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "type", type },
                { "machine_target", machineTarget }
            };
        }

        private static object AddScriptNode(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_script_node");
            EnsureVSAvailable();

            string graphPath = GetStringParam(p, "graph_path");
            string nodeType = GetStringParam(p, "node_type");
            string posStr = GetStringParam(p, "position", "0,0");
            string nodeId = GetStringParam(p, "node_id");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(nodeType))
                throw new ArgumentException("node_type is required");

            if (!NodeTypeMap.TryGetValue(nodeType, out string vsTypeName))
                throw new ArgumentException($"Unknown node type: {nodeType}. Supported: {string.Join(", ", NodeTypeMap.Keys)}");

            var pos = ParsePosition(posStr);

            // Load the graph asset
            var asset = AssetDatabase.LoadMainAssetAtPath(graphPath);
            if (asset == null)
                throw new ArgumentException($"Visual Script asset not found at: {graphPath}");

            // Get the graph from the asset
            var graphProp = asset.GetType().GetProperty("graph");
            if (graphProp == null)
                throw new InvalidOperationException("Could not access graph property on the asset");

            var graph = graphProp.GetValue(asset);
            if (graph == null)
                throw new InvalidOperationException("Graph is null on the asset");

            // Create the unit (node)
            Type unitType = FindVSType(vsTypeName.Replace("Unity.VisualScripting.", ""));
            if (unitType == null)
            {
                // Try full name search
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    unitType = asm.GetType(vsTypeName, false);
                    if (unitType != null) break;
                }
            }

            if (unitType == null)
                throw new InvalidOperationException($"Could not find Visual Scripting unit type: {vsTypeName}");

            object unit;

            // InvokeMember nodes need special construction
            if (InvokeMemberMap.TryGetValue(nodeType, out var memberInfo))
            {
                var targetTypeObj = Type.GetType(memberInfo.targetType + ", UnityEngine") ??
                                    Type.GetType(memberInfo.targetType + ", UnityEngine.CoreModule");
                if (targetTypeObj != null)
                {
                    var methods = targetTypeObj.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => m.Name == memberInfo.methodName)
                        .ToArray();

                    if (methods.Length > 0)
                    {
                        // Create InvokeMember with the target method
                        var constructor = unitType.GetConstructors()
                            .FirstOrDefault(c => c.GetParameters().Length > 0);
                        if (constructor != null)
                        {
                            var paramTypes = constructor.GetParameters();
                            if (paramTypes.Length == 1 && paramTypes[0].ParameterType == typeof(MethodInfo))
                            {
                                unit = constructor.Invoke(new object[] { methods[0] });
                            }
                            else
                            {
                                unit = Activator.CreateInstance(unitType);
                            }
                        }
                        else
                        {
                            unit = Activator.CreateInstance(unitType);
                        }
                    }
                    else
                    {
                        unit = Activator.CreateInstance(unitType);
                    }
                }
                else
                {
                    unit = Activator.CreateInstance(unitType);
                }
            }
            else
            {
                unit = Activator.CreateInstance(unitType);
            }

            // Set position
            var positionProp = unit.GetType().GetProperty("position");
            if (positionProp != null)
            {
                positionProp.SetValue(unit, pos);
            }

            // Add unit to graph
            var unitsProperty = graph.GetType().GetProperty("units");
            if (unitsProperty != null)
            {
                var units = unitsProperty.GetValue(graph);
                var addMethod = units.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    addMethod.Invoke(units, new[] { unit });
                }
            }

            // Generate a node ID for reference
            if (string.IsNullOrEmpty(nodeId))
            {
                var guidProp = unit.GetType().GetProperty("guid");
                if (guidProp != null)
                {
                    nodeId = guidProp.GetValue(unit)?.ToString();
                }
                if (string.IsNullOrEmpty(nodeId))
                {
                    nodeId = nodeType.ToLower() + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "node_id", nodeId },
                { "node_type", nodeType },
                { "position", posStr }
            };
        }

        private static object ConnectScriptNodes(Dictionary<string, object> p)
        {
            ThrowIfPlaying("connect_script_nodes");
            EnsureVSAvailable();

            string graphPath = GetStringParam(p, "graph_path");
            string fromNodeId = GetStringParam(p, "from_node");
            string fromPort = GetStringParam(p, "from_port");
            string toNodeId = GetStringParam(p, "to_node");
            string toPort = GetStringParam(p, "to_port");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(fromNodeId))
                throw new ArgumentException("from_node is required");
            if (string.IsNullOrEmpty(fromPort))
                throw new ArgumentException("from_port is required");
            if (string.IsNullOrEmpty(toNodeId))
                throw new ArgumentException("to_node is required");
            if (string.IsNullOrEmpty(toPort))
                throw new ArgumentException("to_port is required");

            var asset = AssetDatabase.LoadMainAssetAtPath(graphPath);
            if (asset == null)
                throw new ArgumentException($"Visual Script asset not found at: {graphPath}");

            var graphProp = asset.GetType().GetProperty("graph");
            var graph = graphProp?.GetValue(asset);
            if (graph == null)
                throw new InvalidOperationException("Could not access graph");

            var unitsProperty = graph.GetType().GetProperty("units");
            if (unitsProperty == null)
                throw new InvalidOperationException("Could not access units on graph");

            var units = unitsProperty.GetValue(graph);
            var unitList = (units as System.Collections.IEnumerable)?.Cast<object>().ToList();
            if (unitList == null || unitList.Count == 0)
                throw new InvalidOperationException("No nodes found in graph");

            // Find source and target units by guid or index
            object sourceUnit = FindUnitById(unitList, fromNodeId);
            object targetUnit = FindUnitById(unitList, toNodeId);

            if (sourceUnit == null)
                throw new ArgumentException($"Source node not found: {fromNodeId}");
            if (targetUnit == null)
                throw new ArgumentException($"Target node not found: {toNodeId}");

            // Find output port on source
            object outputPort = FindPort(sourceUnit, fromPort, isOutput: true);
            if (outputPort == null)
                throw new ArgumentException($"Output port '{fromPort}' not found on node '{fromNodeId}'");

            // Find input port on target
            object inputPort = FindPort(targetUnit, toPort, isOutput: false);
            if (inputPort == null)
                throw new ArgumentException($"Input port '{toPort}' not found on node '{toNodeId}'");

            // Determine if this is a control (flow) or value connection
            bool isControlPort = IsControlPort(outputPort);

            if (isControlPort)
            {
                // Connect control ports
                var connectMethod = outputPort.GetType().GetMethod("ConnectToAny") ??
                                    outputPort.GetType().GetMethod("Connect");
                if (connectMethod != null)
                {
                    connectMethod.Invoke(outputPort, new[] { inputPort });
                }
                else
                {
                    throw new InvalidOperationException("Could not find connection method on control output port");
                }
            }
            else
            {
                // Connect value ports — connection goes from input to output in VS
                var connectMethod = inputPort.GetType().GetMethod("ConnectToAny") ??
                                    inputPort.GetType().GetMethod("Connect");
                if (connectMethod != null)
                {
                    connectMethod.Invoke(inputPort, new[] { outputPort });
                }
                else
                {
                    throw new InvalidOperationException("Could not find connection method on value input port");
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "from", $"{fromNodeId}.{fromPort}" },
                { "to", $"{toNodeId}.{toPort}" }
            };
        }

        private static object FindUnitById(List<object> units, string id)
        {
            // Try by index first
            if (int.TryParse(id, out int index) && index >= 0 && index < units.Count)
                return units[index];

            // Try by guid
            foreach (var unit in units)
            {
                var guidProp = unit.GetType().GetProperty("guid");
                if (guidProp != null)
                {
                    string guid = guidProp.GetValue(unit)?.ToString();
                    if (guid == id) return unit;
                }
            }

            // Try matching by type name
            foreach (var unit in units)
            {
                if (unit.GetType().Name.Equals(id, StringComparison.OrdinalIgnoreCase))
                    return unit;
            }

            return null;
        }

        private static object FindPort(object unit, string portName, bool isOutput)
        {
            string propertyName;
            if (isOutput)
            {
                propertyName = "controlOutputs";
                var ports = GetPortCollection(unit, propertyName);
                var port = FindPortByKey(ports, portName);
                if (port != null) return port;

                propertyName = "valueOutputs";
                ports = GetPortCollection(unit, propertyName);
                port = FindPortByKey(ports, portName);
                return port;
            }
            else
            {
                propertyName = "controlInputs";
                var ports = GetPortCollection(unit, propertyName);
                var port = FindPortByKey(ports, portName);
                if (port != null) return port;

                propertyName = "valueInputs";
                ports = GetPortCollection(unit, propertyName);
                port = FindPortByKey(ports, portName);
                return port;
            }
        }

        private static System.Collections.IEnumerable GetPortCollection(object unit, string propertyName)
        {
            var prop = unit.GetType().GetProperty(propertyName);
            if (prop == null) return null;
            return prop.GetValue(unit) as System.Collections.IEnumerable;
        }

        private static object FindPortByKey(System.Collections.IEnumerable ports, string key)
        {
            if (ports == null) return null;
            foreach (var port in ports)
            {
                var keyProp = port.GetType().GetProperty("key");
                if (keyProp != null)
                {
                    string k = keyProp.GetValue(port)?.ToString();
                    if (k != null && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        return port;
                }
            }
            return null;
        }

        private static bool IsControlPort(object port)
        {
            string typeName = port.GetType().Name;
            return typeName.Contains("ControlOutput") || typeName.Contains("ControlInput");
        }

        private static object AddScriptVariable(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_script_variable");
            EnsureVSAvailable();

            string graphPath = GetStringParam(p, "graph_path");
            string variableName = GetStringParam(p, "variable_name");
            string variableType = GetStringParam(p, "variable_type");
            string defaultValue = GetStringParam(p, "default_value");
            string scope = GetStringParam(p, "scope", "Graph");

            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");
            if (string.IsNullOrEmpty(variableName))
                throw new ArgumentException("variable_name is required");
            if (string.IsNullOrEmpty(variableType))
                throw new ArgumentException("variable_type is required");

            var asset = AssetDatabase.LoadMainAssetAtPath(graphPath);
            if (asset == null)
                throw new ArgumentException($"Visual Script asset not found at: {graphPath}");

            // Resolve the C# type for the variable
            Type resolvedType = ResolveVariableType(variableType);
            if (resolvedType == null)
                throw new ArgumentException($"Unknown variable type: {variableType}");

            // Parse default value
            object parsedDefault = ParseDefaultValue(defaultValue, resolvedType);

            if (scope.Equals("Graph", StringComparison.OrdinalIgnoreCase))
            {
                // Add to graph-level variables
                var graphProp = asset.GetType().GetProperty("graph");
                var graph = graphProp?.GetValue(asset);
                if (graph == null)
                    throw new InvalidOperationException("Could not access graph");

                var variablesProp = graph.GetType().GetProperty("variables");
                if (variablesProp != null)
                {
                    var variables = variablesProp.GetValue(graph);
                    // Use the VariableDeclarations collection
                    var varDeclType = FindVSType("VariableDeclaration");
                    if (varDeclType != null)
                    {
                        var decl = Activator.CreateInstance(varDeclType, new object[] { variableName, parsedDefault });
                        var typeProp = varDeclType.GetProperty("typeHandle");
                        if (typeProp != null)
                        {
                            // Set via SerializableType if available
                        }

                        var addMethod = variables.GetType().GetMethod("Set");
                        if (addMethod != null)
                        {
                            addMethod.Invoke(variables, new object[] { variableName, parsedDefault });
                        }
                    }
                    else
                    {
                        // Fallback: try Set(name, value) directly
                        var setMethod = variables.GetType().GetMethod("Set");
                        if (setMethod != null)
                        {
                            setMethod.Invoke(variables, new object[] { variableName, parsedDefault });
                        }
                    }
                }
            }
            else
            {
                // For Object/Scene/Application scope, use Variables API via reflection
                var variablesType = FindVSType("Variables");
                if (variablesType == null)
                    throw new InvalidOperationException("Could not find Variables type");

                MethodInfo scopeMethod = null;
                switch (scope.ToLowerInvariant())
                {
                    case "application":
                        scopeMethod = variablesType.GetProperty("Application", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                        break;
                    case "scene":
                        scopeMethod = variablesType.GetMethod("ActiveScene", BindingFlags.Public | BindingFlags.Static);
                        break;
                }

                if (scopeMethod != null)
                {
                    var declarations = scopeMethod.Invoke(null, null);
                    if (declarations != null)
                    {
                        var setMethod = declarations.GetType().GetMethod("Set");
                        if (setMethod != null)
                        {
                            setMethod.Invoke(declarations, new object[] { variableName, parsedDefault });
                        }
                    }
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "variable_name", variableName },
                { "variable_type", variableType },
                { "scope", scope },
                { "default_value", defaultValue }
            };
        }

        private static Type ResolveVariableType(string typeName)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "float":      return typeof(float);
                case "int":        return typeof(int);
                case "string":     return typeof(string);
                case "bool":       return typeof(bool);
                case "vector3":    return typeof(Vector3);
                case "gameobject": return typeof(GameObject);
                case "object":     return typeof(UnityEngine.Object);
                default:           return null;
            }
        }

        private static object ParseDefaultValue(string value, Type type)
        {
            if (string.IsNullOrEmpty(value)) return GetTypeDefault(type);

            try
            {
                if (type == typeof(float))      return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                if (type == typeof(int))        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                if (type == typeof(string))     return value;
                if (type == typeof(bool))       return bool.Parse(value);
                if (type == typeof(Vector3))    return TypeParser.ParseVector3(value);
            }
            catch
            {
                return GetTypeDefault(type);
            }

            return GetTypeDefault(type);
        }

        private static object GetTypeDefault(Type type)
        {
            if (type == typeof(float))      return 0f;
            if (type == typeof(int))        return 0;
            if (type == typeof(string))     return "";
            if (type == typeof(bool))       return false;
            if (type == typeof(Vector3))    return Vector3.zero;
            return null;
        }

        private static object GetVisualScriptInfo(Dictionary<string, object> p)
        {
            EnsureVSAvailable();

            string graphPath = GetStringParam(p, "graph_path");
            if (string.IsNullOrEmpty(graphPath))
                throw new ArgumentException("graph_path is required");

            var asset = AssetDatabase.LoadMainAssetAtPath(graphPath);
            if (asset == null)
                throw new ArgumentException($"Visual Script asset not found at: {graphPath}");

            var graphProp = asset.GetType().GetProperty("graph");
            var graph = graphProp?.GetValue(asset);
            if (graph == null)
                throw new InvalidOperationException("Could not access graph");

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "graph_path", graphPath },
                { "asset_type", asset.GetType().Name }
            };

            // Gather nodes
            var nodeList = new List<object>();
            var unitsProperty = graph.GetType().GetProperty("units");
            if (unitsProperty != null)
            {
                var units = unitsProperty.GetValue(graph) as System.Collections.IEnumerable;
                if (units != null)
                {
                    int idx = 0;
                    foreach (var unit in units)
                    {
                        var nodeInfo = new Dictionary<string, object>
                        {
                            { "index", idx },
                            { "type", unit.GetType().Name }
                        };

                        // Get guid
                        var guidProp = unit.GetType().GetProperty("guid");
                        if (guidProp != null)
                        {
                            nodeInfo["guid"] = guidProp.GetValue(unit)?.ToString();
                        }

                        // Get position
                        var posProp = unit.GetType().GetProperty("position");
                        if (posProp != null)
                        {
                            var pos = posProp.GetValue(unit);
                            nodeInfo["position"] = pos?.ToString();
                        }

                        // List ports
                        var inputPorts = new List<string>();
                        var outputPorts = new List<string>();

                        CollectPortNames(unit, "controlInputs", inputPorts);
                        CollectPortNames(unit, "valueInputs", inputPorts);
                        CollectPortNames(unit, "controlOutputs", outputPorts);
                        CollectPortNames(unit, "valueOutputs", outputPorts);

                        nodeInfo["input_ports"] = inputPorts;
                        nodeInfo["output_ports"] = outputPorts;

                        nodeList.Add(nodeInfo);
                        idx++;
                    }
                }
            }
            result["nodes"] = nodeList;
            result["node_count"] = nodeList.Count;

            // Gather connections
            var connectionList = new List<object>();
            var connectionsProperty = graph.GetType().GetProperty("connections");
            if (connectionsProperty != null)
            {
                var connections = connectionsProperty.GetValue(graph) as System.Collections.IEnumerable;
                if (connections != null)
                {
                    foreach (var conn in connections)
                    {
                        var connInfo = new Dictionary<string, object>();
                        var sourceProp = conn.GetType().GetProperty("source");
                        var destProp = conn.GetType().GetProperty("destination");

                        if (sourceProp != null)
                        {
                            var source = sourceProp.GetValue(conn);
                            connInfo["source"] = source?.ToString();
                        }
                        if (destProp != null)
                        {
                            var dest = destProp.GetValue(conn);
                            connInfo["destination"] = dest?.ToString();
                        }
                        connectionList.Add(connInfo);
                    }
                }
            }
            result["connections"] = connectionList;
            result["connection_count"] = connectionList.Count;

            // Gather variables
            var variableList = new List<object>();
            var variablesProp = graph.GetType().GetProperty("variables");
            if (variablesProp != null)
            {
                var variables = variablesProp.GetValue(graph) as System.Collections.IEnumerable;
                if (variables != null)
                {
                    foreach (var varDecl in variables)
                    {
                        var varInfo = new Dictionary<string, object>();
                        var nameProp = varDecl.GetType().GetProperty("name");
                        var valueProp = varDecl.GetType().GetProperty("value");

                        if (nameProp != null) varInfo["name"] = nameProp.GetValue(varDecl)?.ToString();
                        if (valueProp != null)
                        {
                            var val = valueProp.GetValue(varDecl);
                            varInfo["value"] = val?.ToString();
                            varInfo["type"] = val?.GetType().Name ?? "null";
                        }
                        variableList.Add(varInfo);
                    }
                }
            }
            result["variables"] = variableList;
            result["variable_count"] = variableList.Count;

            return result;
        }

        private static void CollectPortNames(object unit, string propertyName, List<string> portNames)
        {
            var prop = unit.GetType().GetProperty(propertyName);
            if (prop == null) return;

            var ports = prop.GetValue(unit) as System.Collections.IEnumerable;
            if (ports == null) return;

            foreach (var port in ports)
            {
                var keyProp = port.GetType().GetProperty("key");
                if (keyProp != null)
                {
                    portNames.Add(keyProp.GetValue(port)?.ToString() ?? "?");
                }
            }
        }
    }
}
