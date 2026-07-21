using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class ECSCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_ecs_component", CreateECSComponent);
            router.Register("create_ecs_system", CreateECSSystem);
            router.Register("create_ecs_authoring", CreateECSAuthoring);
            router.Register("get_ecs_info", GetECSInfo);
        }

        private static object CreateECSComponent(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_ecs_component");

            string name = GetStringParam(p, "name");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            bool tagComponent = GetBoolParam(p, "tag_component", false);
            string scriptPath = GetStringParam(p, "script_path", $"Assets/Scripts/ECS/Components/{name}.cs");

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var fieldsList = p.ContainsKey("fields") ? p["fields"] as List<object> : null;

            // Determine which using statements we need
            bool needsMathematics = false;
            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "";
                    if (IsMathType(fieldType))
                        needsMathematics = true;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using Unity.Entities;");
            if (needsMathematics)
                sb.AppendLine("using Unity.Mathematics;");
            sb.AppendLine();
            sb.AppendLine($"public struct {name} : IComponentData");
            sb.AppendLine("{");

            if (!tagComponent && fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "float";
                    if (string.IsNullOrEmpty(fieldName)) continue;

                    sb.AppendLine($"    public {fieldType} {fieldName};");
                }
            }
            else if (!tagComponent)
            {
                sb.AppendLine("    // Add your component fields here");
            }

            sb.AppendLine("}");

            WriteScriptFile(scriptPath, sb.ToString());

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "name", name },
                { "is_tag", tagComponent },
                { "field_count", tagComponent ? 0 : (fieldsList?.Count ?? 0) }
            };
        }

        private static object CreateECSSystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_ecs_system");

            string name = GetStringParam(p, "name");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            string systemType = GetStringParam(p, "system_type", "ISystem");
            string scriptPath = GetStringParam(p, "script_path", $"Assets/Scripts/ECS/Systems/{name}.cs");
            string updateGroup = GetStringParam(p, "update_group");

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var queriesList = p.ContainsKey("queries") ? p["queries"] as List<object> : null;

            // Collect all component types for using statements
            var allComponents = new HashSet<string>();
            bool needsTransforms = false;
            if (queriesList != null)
            {
                foreach (var queryObj in queriesList)
                {
                    var query = queryObj as Dictionary<string, object>;
                    if (query == null) continue;
                    var components = query.ContainsKey("components") ? query["components"] as List<object> : null;
                    if (components != null)
                    {
                        foreach (var c in components)
                        {
                            string compName = c.ToString();
                            allComponents.Add(compName);
                            if (compName == "LocalTransform" || compName == "LocalToWorld" || compName == "Translation" || compName == "Rotation")
                                needsTransforms = true;
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using Unity.Burst;");
            sb.AppendLine("using Unity.Entities;");
            sb.AppendLine("using Unity.Mathematics;");
            if (needsTransforms)
                sb.AppendLine("using Unity.Transforms;");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(updateGroup))
                sb.AppendLine($"[UpdateInGroup(typeof({updateGroup}))]");

            if (systemType == "ISystem")
            {
                sb.AppendLine("[BurstCompile]");
                sb.AppendLine($"public partial struct {name} : ISystem");
                sb.AppendLine("{");
                sb.AppendLine("    [BurstCompile]");
                sb.AppendLine("    public void OnCreate(ref SystemState state)");
                sb.AppendLine("    {");

                // Add required components
                if (allComponents.Count > 0)
                {
                    var compList = string.Join(", ", allComponents.Select(c => $"ComponentType.ReadWrite<{c}>()"));
                    sb.AppendLine($"        state.RequireForUpdate(state.GetEntityQuery({compList}));");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    [BurstCompile]");
                sb.AppendLine("    public void OnUpdate(ref SystemState state)");
                sb.AppendLine("    {");
                sb.AppendLine("        float deltaTime = SystemAPI.Time.DeltaTime;");
                sb.AppendLine();

                if (queriesList != null && queriesList.Count > 0)
                {
                    var query = queriesList[0] as Dictionary<string, object>;
                    if (query != null)
                    {
                        var components = query.ContainsKey("components") ? query["components"] as List<object> : null;
                        string access = query.ContainsKey("access") ? query["access"].ToString() : "ReadWrite";

                        if (components != null && components.Count > 0)
                        {
                            // Generate foreach with RefRW/RefRO
                            var foreachParams = new List<string>();
                            foreach (var c in components)
                            {
                                string compName = c.ToString();
                                string refType = access == "ReadOnly" ? "RefRO" : "RefRW";
                                string paramName = char.ToLower(compName[0]) + compName.Substring(1);
                                foreachParams.Add($"{refType}<{compName}> {paramName}");
                            }

                            sb.AppendLine($"        foreach (var ({string.Join(", ", foreachParams.Select(fp => fp.Split(' ').Last()))}) in");
                            sb.AppendLine($"            SystemAPI.Query<{string.Join(", ", foreachParams.Select(fp => fp.Substring(0, fp.LastIndexOf(' '))))}>())");
                            sb.AppendLine("        {");
                            sb.AppendLine("            // TODO: Implement system logic");
                            sb.AppendLine("        }");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("        // TODO: Implement system logic");
                    sb.AppendLine("        // Example: foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())");
                    sb.AppendLine("        // { transform.ValueRW.Position += ... }");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    [BurstCompile]");
                sb.AppendLine("    public void OnDestroy(ref SystemState state) { }");
                sb.AppendLine("}");
            }
            else // SystemBase
            {
                sb.AppendLine($"public partial class {name} : SystemBase");
                sb.AppendLine("{");
                sb.AppendLine("    protected override void OnCreate()");
                sb.AppendLine("    {");
                sb.AppendLine("        base.OnCreate();");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    protected override void OnUpdate()");
                sb.AppendLine("    {");
                sb.AppendLine("        float deltaTime = SystemAPI.Time.DeltaTime;");
                sb.AppendLine();

                if (queriesList != null && queriesList.Count > 0)
                {
                    sb.AppendLine("        Entities.ForEach((");

                    var query = queriesList[0] as Dictionary<string, object>;
                    if (query != null)
                    {
                        var components = query.ContainsKey("components") ? query["components"] as List<object> : null;
                        string access = query.ContainsKey("access") ? query["access"].ToString() : "ReadWrite";

                        if (components != null)
                        {
                            var paramList = new List<string>();
                            foreach (var c in components)
                            {
                                string compName = c.ToString();
                                string paramName = char.ToLower(compName[0]) + compName.Substring(1);
                                string refKeyword = access == "ReadOnly" ? "in" : "ref";
                                paramList.Add($"            {refKeyword} {compName} {paramName}");
                            }
                            sb.AppendLine(string.Join(",\n", paramList));
                        }
                    }

                    sb.AppendLine("        ) =>");
                    sb.AppendLine("        {");
                    sb.AppendLine("            // TODO: Implement system logic");
                    sb.AppendLine("        }).ScheduleParallel();");
                }
                else
                {
                    sb.AppendLine("        // TODO: Implement system logic");
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");
            }

            WriteScriptFile(scriptPath, sb.ToString());

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "name", name },
                { "system_type", systemType },
                { "update_group", updateGroup ?? "(default)" }
            };
        }

        private static object CreateECSAuthoring(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_ecs_authoring");

            string name = GetStringParam(p, "name");
            string componentName = GetStringParam(p, "component_name");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");
            if (string.IsNullOrEmpty(componentName))
                throw new ArgumentException("component_name is required");

            string scriptPath = GetStringParam(p, "script_path", $"Assets/Scripts/ECS/Authoring/{name}.cs");
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var fieldsList = p.ContainsKey("fields") ? p["fields"] as List<object> : null;

            var sb = new StringBuilder();
            sb.AppendLine("using Unity.Entities;");
            sb.AppendLine("using Unity.Mathematics;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {name} : MonoBehaviour");
            sb.AppendLine("{");

            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "float";
                    if (string.IsNullOrEmpty(fieldName)) continue;

                    sb.AppendLine($"    public {fieldType} {fieldName};");
                }
            }
            else
            {
                sb.AppendLine("    // Add authoring fields here");
            }

            sb.AppendLine();
            sb.AppendLine($"    public class {name}Baker : Baker<{name}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override void Bake({name} authoring)");
            sb.AppendLine("        {");
            sb.AppendLine("            var entity = GetEntity(TransformUsageFlags.Dynamic);");
            sb.AppendLine($"            AddComponent(entity, new {componentName}");
            sb.AppendLine("            {");

            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "float";
                    if (string.IsNullOrEmpty(fieldName)) continue;

                    if (fieldType == "GameObject")
                    {
                        sb.AppendLine($"                {fieldName} = GetEntity(authoring.{fieldName}, TransformUsageFlags.Dynamic),");
                    }
                    else
                    {
                        sb.AppendLine($"                {fieldName} = authoring.{fieldName},");
                    }
                }
            }

            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            WriteScriptFile(scriptPath, sb.ToString());

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "authoring_name", name },
                { "component_name", componentName },
                { "field_count", fieldsList?.Count ?? 0 }
            };
        }

        private static object GetECSInfo(Dictionary<string, object> p)
        {
            var componentTypes = new List<object>();
            var systemTypes = new List<object>();
            bool entitiesPackageFound = false;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.FullName.Contains("Unity.Entities"))
                        entitiesPackageFound = true;

                    foreach (var type in assembly.GetTypes())
                    {
                        // Skip Unity/System internal types
                        if (type.Namespace != null && (type.Namespace.StartsWith("Unity.") || type.Namespace.StartsWith("System.")))
                            continue;

                        // Check for IComponentData
                        foreach (var iface in type.GetInterfaces())
                        {
                            if (iface.Name == "IComponentData")
                            {
                                componentTypes.Add(new Dictionary<string, object>
                                {
                                    { "name", type.Name },
                                    { "namespace", type.Namespace ?? "" },
                                    { "assembly", assembly.GetName().Name },
                                    { "is_tag", type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Length == 0 }
                                });
                                break;
                            }
                        }

                        // Check for SystemBase / ISystem
                        if (type.BaseType != null && type.BaseType.Name == "SystemBase" && !type.IsAbstract)
                        {
                            systemTypes.Add(new Dictionary<string, object>
                            {
                                { "name", type.Name },
                                { "namespace", type.Namespace ?? "" },
                                { "assembly", assembly.GetName().Name },
                                { "type", "SystemBase" }
                            });
                        }

                        foreach (var iface in type.GetInterfaces())
                        {
                            if (iface.Name == "ISystem" && !type.IsAbstract)
                            {
                                // Avoid duplicating SystemBase entries
                                if (type.BaseType != null && type.BaseType.Name == "SystemBase") break;
                                systemTypes.Add(new Dictionary<string, object>
                                {
                                    { "name", type.Name },
                                    { "namespace", type.Namespace ?? "" },
                                    { "assembly", assembly.GetName().Name },
                                    { "type", "ISystem" }
                                });
                                break;
                            }
                        }
                    }
                }
                catch { /* Skip assemblies that fail to enumerate */ }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "entities_package_installed", entitiesPackageFound },
                { "component_count", componentTypes.Count },
                { "components", componentTypes },
                { "system_count", systemTypes.Count },
                { "systems", systemTypes }
            };
        }

        private static bool IsMathType(string type)
        {
            return type == "float2" || type == "float3" || type == "float4" ||
                   type == "int2" || type == "int3" || type == "int4" ||
                   type == "quaternion" || type == "float4x4" || type == "float3x3";
        }

        private static void WriteScriptFile(string scriptPath, string content)
        {
            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();
        }
    }
}
