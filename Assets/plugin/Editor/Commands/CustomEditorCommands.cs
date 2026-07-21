using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class CustomEditorCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_custom_inspector", CreateCustomInspector);
            router.Register("create_editor_window", CreateEditorWindow);
            router.Register("create_property_drawer", CreatePropertyDrawer);
            router.Register("create_scriptable_wizard", CreateScriptableWizard);
        }

        private static object CreateCustomInspector(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_custom_inspector");

            string targetScript = GetStringParam(p, "target_script");
            if (string.IsNullOrEmpty(targetScript))
                throw new ArgumentException("target_script is required");

            // Resolve the target class name
            string className = Path.GetFileNameWithoutExtension(targetScript);
            Type targetType = null;

            // Try to find the type in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == className && typeof(MonoBehaviour).IsAssignableFrom(type))
                    {
                        targetType = type;
                        break;
                    }
                }
                if (targetType != null) break;
            }

            string scriptPath = GetStringParam(p, "script_path", $"Assets/Editor/{className}Editor.cs");
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            // Get field customizations
            var fieldsList = p.ContainsKey("fields") ? p["fields"] as List<object> : null;

            // Discover serialized fields from the target type
            var discoveredFields = new List<Dictionary<string, string>>();
            if (targetType != null)
            {
                var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    bool isSerialized = field.IsPublic ||
                        field.GetCustomAttribute<SerializeField>() != null;
                    bool isHidden = field.GetCustomAttribute<HideInInspector>() != null ||
                        field.GetCustomAttribute<NonSerializedAttribute>() != null;

                    if (isSerialized && !isHidden)
                    {
                        discoveredFields.Add(new Dictionary<string, string>
                        {
                            { "name", field.Name },
                            { "fieldType", field.FieldType.Name }
                        });
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"[CustomEditor(typeof({className}))]");
            sb.AppendLine($"public class {className}Editor : Editor");
            sb.AppendLine("{");

            // Declare SerializedProperty fields
            if (fieldsList != null && fieldsList.Count > 0)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "default";
                    if (string.IsNullOrEmpty(fieldName)) continue;
                    if (fieldType == "space" || fieldType == "header" || fieldType == "button") continue;
                    sb.AppendLine($"    private SerializedProperty _{fieldName};");
                }

                // Foldout booleans
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "default";
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    if (fieldType == "foldout")
                        sb.AppendLine($"    private bool _show{fieldName} = true;");
                }
            }
            else
            {
                // Auto-generate from discovered fields
                foreach (var field in discoveredFields)
                {
                    sb.AppendLine($"    private SerializedProperty _{field["name"]};");
                }
            }

            sb.AppendLine();
            sb.AppendLine("    private void OnEnable()");
            sb.AppendLine("    {");

            if (fieldsList != null && fieldsList.Count > 0)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "default";
                    if (string.IsNullOrEmpty(fieldName) || fieldType == "space" || fieldType == "header" || fieldType == "button") continue;
                    sb.AppendLine($"        _{fieldName} = serializedObject.FindProperty(\"{fieldName}\");");
                }
            }
            else
            {
                foreach (var field in discoveredFields)
                {
                    sb.AppendLine($"        _{field["name"]} = serializedObject.FindProperty(\"{field["name"]}\");");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnInspectorGUI()");
            sb.AppendLine("    {");
            sb.AppendLine("        serializedObject.Update();");
            sb.AppendLine();

            if (fieldsList != null && fieldsList.Count > 0)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "default";
                    string label = field.ContainsKey("label") ? field["label"].ToString() : fieldName;

                    switch (fieldType)
                    {
                        case "slider":
                            float min = field.ContainsKey("min") ? Convert.ToSingle(field["min"]) : 0f;
                            float max = field.ContainsKey("max") ? Convert.ToSingle(field["max"]) : 1f;
                            sb.AppendLine($"        EditorGUILayout.Slider(_{fieldName}, {min}f, {max}f, new GUIContent(\"{label}\"));");
                            break;
                        case "color":
                            sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName}, new GUIContent(\"{label}\"));");
                            break;
                        case "gradient":
                            sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName}, new GUIContent(\"{label}\"));");
                            break;
                        case "reorderable_list":
                            sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName}, new GUIContent(\"{label}\"), true);");
                            break;
                        case "foldout":
                            sb.AppendLine($"        _show{fieldName} = EditorGUILayout.Foldout(_show{fieldName}, \"{label}\", true);");
                            sb.AppendLine($"        if (_show{fieldName})");
                            sb.AppendLine("        {");
                            sb.AppendLine("            EditorGUI.indentLevel++;");
                            sb.AppendLine($"            EditorGUILayout.PropertyField(_{fieldName}, true);");
                            sb.AppendLine("            EditorGUI.indentLevel--;");
                            sb.AppendLine("        }");
                            break;
                        case "button":
                            sb.AppendLine($"        if (GUILayout.Button(\"{label}\"))");
                            sb.AppendLine("        {");
                            sb.AppendLine($"            (({className})target).{fieldName}();");
                            sb.AppendLine("        }");
                            break;
                        case "space":
                            sb.AppendLine("        EditorGUILayout.Space();");
                            break;
                        case "header":
                            sb.AppendLine($"        EditorGUILayout.LabelField(\"{label}\", EditorStyles.boldLabel);");
                            break;
                        default:
                            sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName}, new GUIContent(\"{label}\"));");
                            break;
                    }
                }
            }
            else
            {
                // Auto-generated fields
                foreach (var field in discoveredFields)
                {
                    string fieldName = field["name"];
                    string fieldType = field["fieldType"];

                    if (fieldType == "Single" || fieldType == "Int32")
                        sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName});");
                    else if (fieldType == "Color")
                        sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName});");
                    else
                        sb.AppendLine($"        EditorGUILayout.PropertyField(_{fieldName}, true);");
                }

                if (discoveredFields.Count == 0)
                {
                    sb.AppendLine("        DrawDefaultInspector();");
                }
            }

            sb.AppendLine();
            sb.AppendLine("        serializedObject.ApplyModifiedProperties();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "target_class", className },
                { "discovered_fields", discoveredFields.Count },
                { "custom_fields", fieldsList?.Count ?? 0 }
            };
        }

        private static object CreateEditorWindow(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_editor_window");

            string title = GetStringParam(p, "title");
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("title is required");

            string safeClassName = title.Replace(" ", "");
            string scriptPath = GetStringParam(p, "script_path", $"Assets/Editor/{safeClassName}Window.cs");
            string menuPath = GetStringParam(p, "menu_path", $"Window/{title}");
            string sizeStr = GetStringParam(p, "size", "400,300");

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var sizeParts = sizeStr.Split(',');
            int width = sizeParts.Length > 0 ? int.Parse(sizeParts[0].Trim()) : 400;
            int height = sizeParts.Length > 1 ? int.Parse(sizeParts[1].Trim()) : 300;

            var fieldsList = p.ContainsKey("fields") ? p["fields"] as List<object> : null;

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {safeClassName}Window : EditorWindow");
            sb.AppendLine("{");

            // Declare fields
            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "text_field";
                    if (string.IsNullOrEmpty(fieldName) || fieldType == "button" || fieldType == "label") continue;

                    switch (fieldType)
                    {
                        case "text_field":
                        case "text_area":
                            sb.AppendLine($"    private string {fieldName} = \"\";");
                            break;
                        case "int_field":
                            sb.AppendLine($"    private int {fieldName};");
                            break;
                        case "float_field":
                            sb.AppendLine($"    private float {fieldName};");
                            break;
                        case "toggle":
                            sb.AppendLine($"    private bool {fieldName};");
                            break;
                        case "popup":
                            sb.AppendLine($"    private int {fieldName}Index;");
                            var options = field.ContainsKey("options") ? field["options"] as List<object> : null;
                            if (options != null)
                            {
                                var optStr = string.Join("\", \"", options.Select(o => o.ToString()));
                                sb.AppendLine($"    private string[] {fieldName}Options = new string[] {{ \"{optStr}\" }};");
                            }
                            break;
                        case "color_field":
                            sb.AppendLine($"    private Color {fieldName} = Color.white;");
                            break;
                        case "vector3_field":
                            sb.AppendLine($"    private Vector3 {fieldName};");
                            break;
                        case "object_field":
                            string objectType = field.ContainsKey("object_type") ? field["object_type"].ToString() : "Object";
                            sb.AppendLine($"    private {objectType} {fieldName};");
                            break;
                        case "enum_field":
                            sb.AppendLine($"    private int {fieldName};");
                            break;
                    }
                }
            }

            sb.AppendLine($"    private Vector2 scrollPosition;");
            sb.AppendLine();
            sb.AppendLine($"    [MenuItem(\"{menuPath}\")]");
            sb.AppendLine($"    public static void ShowWindow()");
            sb.AppendLine("    {");
            sb.AppendLine($"        var window = GetWindow<{safeClassName}Window>(\"{title}\");");
            sb.AppendLine($"        window.minSize = new Vector2({width}, {height});");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void OnGUI()");
            sb.AppendLine("    {");
            sb.AppendLine("        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);");
            sb.AppendLine("        EditorGUILayout.Space(5);");

            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "text_field";
                    string label = field.ContainsKey("label") ? field["label"].ToString() : fieldName;

                    switch (fieldType)
                    {
                        case "text_field":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.TextField(\"{label}\", {fieldName});");
                            break;
                        case "text_area":
                            sb.AppendLine($"        EditorGUILayout.LabelField(\"{label}\");");
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.TextArea({fieldName}, GUILayout.Height(60));");
                            break;
                        case "int_field":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.IntField(\"{label}\", {fieldName});");
                            break;
                        case "float_field":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.FloatField(\"{label}\", {fieldName});");
                            break;
                        case "toggle":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.Toggle(\"{label}\", {fieldName});");
                            break;
                        case "popup":
                            sb.AppendLine($"        {fieldName}Index = EditorGUILayout.Popup(\"{label}\", {fieldName}Index, {fieldName}Options);");
                            break;
                        case "button":
                            sb.AppendLine($"        if (GUILayout.Button(\"{label}\"))");
                            sb.AppendLine("        {");
                            sb.AppendLine($"            // TODO: Implement {fieldName} action");
                            sb.AppendLine($"            Debug.Log(\"{label} clicked\");");
                            sb.AppendLine("        }");
                            break;
                        case "label":
                            sb.AppendLine($"        EditorGUILayout.LabelField(\"{label}\", EditorStyles.boldLabel);");
                            break;
                        case "color_field":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.ColorField(\"{label}\", {fieldName});");
                            break;
                        case "vector3_field":
                            sb.AppendLine($"        {fieldName} = EditorGUILayout.Vector3Field(\"{label}\", {fieldName});");
                            break;
                        case "object_field":
                            string objectType = field.ContainsKey("object_type") ? field["object_type"].ToString() : "Object";
                            sb.AppendLine($"        {fieldName} = ({objectType})EditorGUILayout.ObjectField(\"{label}\", {fieldName}, typeof({objectType}), true);");
                            break;
                    }

                    sb.AppendLine("        EditorGUILayout.Space(2);");
                }
            }

            sb.AppendLine("        EditorGUILayout.EndScrollView();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "title", title },
                { "menu_path", menuPath },
                { "size", $"{width}x{height}" }
            };
        }

        private static object CreatePropertyDrawer(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_property_drawer");

            string targetType = GetStringParam(p, "target_type");
            if (string.IsNullOrEmpty(targetType))
                throw new ArgumentException("target_type is required");

            string scriptPath = GetStringParam(p, "script_path", $"Assets/Editor/{targetType}Drawer.cs");
            string layout = GetStringParam(p, "layout", "single_line");
            string[] fields = GetStringListParam(p, "fields");

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"[CustomPropertyDrawer(typeof({targetType}))]");
            sb.AppendLine($"public class {targetType}Drawer : PropertyDrawer");
            sb.AppendLine("{");

            if (layout == "single_line")
            {
                sb.AppendLine("    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        EditorGUI.BeginProperty(position, label, property);");
                sb.AppendLine("        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);");
                sb.AppendLine();
                sb.AppendLine("        var indent = EditorGUI.indentLevel;");
                sb.AppendLine("        EditorGUI.indentLevel = 0;");
                sb.AppendLine();

                if (fields != null && fields.Length > 0)
                {
                    sb.AppendLine($"        float fieldWidth = (position.width - {(fields.Length - 1) * 5}f) / {fields.Length}f;");
                    sb.AppendLine($"        float x = position.x;");
                    sb.AppendLine();

                    foreach (var fieldName in fields)
                    {
                        sb.AppendLine($"        EditorGUI.PropertyField(");
                        sb.AppendLine($"            new Rect(x, position.y, fieldWidth, position.height),");
                        sb.AppendLine($"            property.FindPropertyRelative(\"{fieldName}\"), GUIContent.none);");
                        sb.AppendLine($"        x += fieldWidth + 5f;");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("        // Auto-draw all visible children in a single line");
                    sb.AppendLine("        var iter = property.Copy();");
                    sb.AppendLine("        var end = property.GetEndProperty();");
                    sb.AppendLine("        iter.NextVisible(true);");
                    sb.AppendLine("        int count = 0;");
                    sb.AppendLine("        var tempIter = property.Copy();");
                    sb.AppendLine("        tempIter.NextVisible(true);");
                    sb.AppendLine("        while (!SerializedProperty.EqualContents(tempIter, end)) { count++; if (!tempIter.NextVisible(false)) break; }");
                    sb.AppendLine("        if (count == 0) count = 1;");
                    sb.AppendLine("        float fieldWidth = (position.width - (count - 1) * 5f) / count;");
                    sb.AppendLine("        float x = position.x;");
                    sb.AppendLine("        do");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (SerializedProperty.EqualContents(iter, end)) break;");
                    sb.AppendLine("            EditorGUI.PropertyField(");
                    sb.AppendLine("                new Rect(x, position.y, fieldWidth, position.height),");
                    sb.AppendLine("                iter, GUIContent.none);");
                    sb.AppendLine("            x += fieldWidth + 5f;");
                    sb.AppendLine("        } while (iter.NextVisible(false));");
                }

                sb.AppendLine();
                sb.AppendLine("        EditorGUI.indentLevel = indent;");
                sb.AppendLine("        EditorGUI.EndProperty();");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        return EditorGUIUtility.singleLineHeight;");
                sb.AppendLine("    }");
            }
            else if (layout == "multi_line")
            {
                sb.AppendLine("    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        EditorGUI.BeginProperty(position, label, property);");
                sb.AppendLine("        property.isExpanded = EditorGUI.Foldout(");
                sb.AppendLine("            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),");
                sb.AppendLine("            property.isExpanded, label, true);");
                sb.AppendLine();
                sb.AppendLine("        if (property.isExpanded)");
                sb.AppendLine("        {");
                sb.AppendLine("            EditorGUI.indentLevel++;");
                sb.AppendLine("            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;");
                sb.AppendLine("            var iter = property.Copy();");
                sb.AppendLine("            var end = property.GetEndProperty();");
                sb.AppendLine("            iter.NextVisible(true);");
                sb.AppendLine("            do");
                sb.AppendLine("            {");
                sb.AppendLine("                if (SerializedProperty.EqualContents(iter, end)) break;");
                sb.AppendLine("                float h = EditorGUI.GetPropertyHeight(iter, true);");
                sb.AppendLine("                EditorGUI.PropertyField(");
                sb.AppendLine("                    new Rect(position.x, y, position.width, h), iter, true);");
                sb.AppendLine("                y += h + EditorGUIUtility.standardVerticalSpacing;");
                sb.AppendLine("            } while (iter.NextVisible(false));");
                sb.AppendLine("            EditorGUI.indentLevel--;");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        EditorGUI.EndProperty();");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        if (!property.isExpanded)");
                sb.AppendLine("            return EditorGUIUtility.singleLineHeight;");
                sb.AppendLine();
                sb.AppendLine("        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;");
                sb.AppendLine("        var iter = property.Copy();");
                sb.AppendLine("        var end = property.GetEndProperty();");
                sb.AppendLine("        iter.NextVisible(true);");
                sb.AppendLine("        do");
                sb.AppendLine("        {");
                sb.AppendLine("            if (SerializedProperty.EqualContents(iter, end)) break;");
                sb.AppendLine("            height += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;");
                sb.AppendLine("        } while (iter.NextVisible(false));");
                sb.AppendLine("        return height;");
                sb.AppendLine("    }");
            }
            else // custom
            {
                sb.AppendLine("    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        EditorGUI.BeginProperty(position, label, property);");
                sb.AppendLine("        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);");
                sb.AppendLine();
                sb.AppendLine("        // TODO: Implement custom drawing logic");
                sb.AppendLine("        EditorGUI.LabelField(position, \"Custom Drawer - Implement OnGUI\");");
                sb.AppendLine();
                sb.AppendLine("        EditorGUI.EndProperty();");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)");
                sb.AppendLine("    {");
                sb.AppendLine("        return EditorGUIUtility.singleLineHeight;");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "target_type", targetType },
                { "layout", layout }
            };
        }

        private static object CreateScriptableWizard(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_scriptable_wizard");

            string title = GetStringParam(p, "title");
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("title is required");

            string safeClassName = title.Replace(" ", "") + "Wizard";
            string scriptPath = GetStringParam(p, "script_path", $"Assets/Editor/{safeClassName}.cs");
            string menuPath = GetStringParam(p, "menu_path", $"Tools/{title}");
            string createButtonText = GetStringParam(p, "create_button_text", "Create");
            string otherButtonText = GetStringParam(p, "other_button_text");

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var fieldsList = p.ContainsKey("fields") ? p["fields"] as List<object> : null;

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {safeClassName} : ScriptableWizard");
            sb.AppendLine("{");

            // Declare fields
            if (fieldsList != null)
            {
                foreach (var fieldObj in fieldsList)
                {
                    var field = fieldObj as Dictionary<string, object>;
                    if (field == null) continue;
                    string fieldName = field.ContainsKey("name") ? field["name"].ToString() : "";
                    string fieldType = field.ContainsKey("type") ? field["type"].ToString() : "string";
                    string tooltip = field.ContainsKey("tooltip") ? field["tooltip"].ToString() : null;
                    string defaultValue = field.ContainsKey("default_value") ? field["default_value"].ToString() : null;

                    if (string.IsNullOrEmpty(fieldName)) continue;

                    if (!string.IsNullOrEmpty(tooltip))
                        sb.AppendLine($"    [Tooltip(\"{tooltip}\")]");

                    string csType = MapFieldType(fieldType);
                    string defaultStr = GetDefaultValueString(csType, defaultValue);
                    sb.AppendLine($"    public {csType} {fieldName}{defaultStr};");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"    [MenuItem(\"{menuPath}\")]");
            sb.AppendLine($"    static void Open()");
            sb.AppendLine("    {");

            if (!string.IsNullOrEmpty(otherButtonText))
                sb.AppendLine($"        DisplayWizard<{safeClassName}>(\"{title}\", \"{createButtonText}\", \"{otherButtonText}\");");
            else
                sb.AppendLine($"        DisplayWizard<{safeClassName}>(\"{title}\", \"{createButtonText}\");");

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void OnWizardCreate()");
            sb.AppendLine("    {");
            sb.AppendLine($"        // TODO: Implement create action");
            sb.AppendLine($"        Debug.Log(\"{title}: Create action executed\");");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void OnWizardUpdate()");
            sb.AppendLine("    {");
            sb.AppendLine("        helpString = \"Configure the settings and click the button to execute.\";");
            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(otherButtonText))
            {
                sb.AppendLine();
                sb.AppendLine("    private void OnWizardOtherButton()");
                sb.AppendLine("    {");
                sb.AppendLine($"        // TODO: Implement '{otherButtonText}' action");
                sb.AppendLine($"        Debug.Log(\"{title}: Other button action executed\");");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", scriptPath },
                { "title", title },
                { "menu_path", menuPath }
            };
        }

        private static string MapFieldType(string type)
        {
            switch (type.ToLower())
            {
                case "string": return "string";
                case "int": return "int";
                case "float": return "float";
                case "bool": return "bool";
                case "color": return "Color";
                case "vector3": return "Vector3";
                case "gameobject": return "GameObject";
                case "material": return "Material";
                case "object": return "UnityEngine.Object";
                default: return type;
            }
        }

        private static string GetDefaultValueString(string csType, string defaultValue)
        {
            if (string.IsNullOrEmpty(defaultValue)) return "";

            switch (csType)
            {
                case "string": return $" = \"{defaultValue}\"";
                case "int": return $" = {defaultValue}";
                case "float": return $" = {defaultValue}f";
                case "bool": return $" = {defaultValue.ToLower()}";
                default: return "";
            }
        }
    }
}
