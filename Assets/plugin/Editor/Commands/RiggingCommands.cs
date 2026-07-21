using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class RiggingCommands : BaseCommand
    {
        private const string RigBuilderTypeName = "UnityEngine.Animations.Rigging.RigBuilder, Unity.Animation.Rigging";
        private const string RigTypeName = "UnityEngine.Animations.Rigging.Rig, Unity.Animation.Rigging";

        public static void Register(CommandRouter router)
        {
            router.Register("add_rig_constraint", AddRigConstraint);
            router.Register("setup_ik", SetupIK);
            router.Register("get_rig_info", GetRigInfo);
            router.Register("add_rig_layer", AddRigLayer);
        }

        private static Type GetRiggingType(string typeName)
        {
            // Try full assembly-qualified name
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Search all assemblies
            string shortName = typeName.Contains(",") ? typeName.Split(',')[0].Trim() : typeName;
            string className = shortName.Contains(".") ? shortName.Substring(shortName.LastIndexOf('.') + 1) : shortName;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.FullName.Contains("Animation") && !assembly.FullName.Contains("Rigging"))
                    continue;
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == className || t.FullName == shortName)
                        return t;
                }
            }
            return null;
        }

        private static Type GetConstraintType(string constraintType)
        {
            string fullName = $"UnityEngine.Animations.Rigging.{constraintType}Constraint";
            // For types like "TwoBoneIK" -> "TwoBoneIKConstraint"
            if (!constraintType.EndsWith("Constraint"))
                fullName = $"UnityEngine.Animations.Rigging.{constraintType}Constraint";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.FullName == fullName || t.Name == constraintType + "Constraint" || t.Name == constraintType)
                        return t;
                }
            }
            return null;
        }

        private static Component EnsureRigBuilder(GameObject root)
        {
            var rigBuilderType = GetRiggingType(RigBuilderTypeName);
            if (rigBuilderType == null)
                throw new InvalidOperationException(
                    "Animation Rigging package not found. Install 'com.unity.animation.rigging' via Package Manager.");

            var rigBuilder = root.GetComponent(rigBuilderType);
            if (rigBuilder == null)
                rigBuilder = Undo.AddComponent(root, rigBuilderType);
            return rigBuilder;
        }

        private static Transform EnsureRigLayer(GameObject root, string rigName = "Rig")
        {
            var rigType = GetRiggingType(RigTypeName);
            if (rigType == null)
                throw new InvalidOperationException("Rig type not found. Animation Rigging package may not be installed.");

            // Look for existing rig child
            foreach (Transform child in root.transform)
            {
                if (child.GetComponent(rigType) != null)
                    return child;
            }

            // Create new rig
            var rigGo = new GameObject(rigName);
            Undo.RegisterCreatedObjectUndo(rigGo, "Create Rig");
            rigGo.transform.SetParent(root.transform, false);
            Undo.AddComponent(rigGo, rigType);

            // Add rig to RigBuilder layers
            var rigBuilder = EnsureRigBuilder(root);
            var layersProp = rigBuilder.GetType().GetProperty("layers") ??
                             rigBuilder.GetType().GetProperty("RigLayers");
            if (layersProp != null)
            {
                var layers = layersProp.GetValue(rigBuilder);
                var addMethod = layers.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    // Create RigLayer struct/class
                    var rigLayerType = Type.GetType("UnityEngine.Animations.Rigging.RigLayer, Unity.Animation.Rigging");
                    if (rigLayerType == null)
                    {
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            foreach (var t in assembly.GetTypes())
                            {
                                if (t.Name == "RigLayer")
                                {
                                    rigLayerType = t;
                                    break;
                                }
                            }
                            if (rigLayerType != null) break;
                        }
                    }

                    if (rigLayerType != null)
                    {
                        var rigComponent = rigGo.GetComponent(GetRiggingType(RigTypeName));
                        var ctor = rigLayerType.GetConstructor(new[] { GetRiggingType(RigTypeName), typeof(bool) });
                        if (ctor != null)
                        {
                            var layer = ctor.Invoke(new object[] { rigComponent, true });
                            addMethod.Invoke(layers, new[] { layer });
                        }
                        else
                        {
                            ctor = rigLayerType.GetConstructor(new[] { GetRiggingType(RigTypeName) });
                            if (ctor != null)
                            {
                                var layer = ctor.Invoke(new object[] { rigComponent });
                                addMethod.Invoke(layers, new[] { layer });
                            }
                        }
                    }
                }
            }

            return rigGo.transform;
        }

        private static object AddRigConstraint(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_rig_constraint");

            string targetPath = GetStringParam(p, "target");
            string constraintTypeName = GetStringParam(p, "constraint_type");
            string constraintTargetPath = GetStringParam(p, "constraint_target");
            string sourcePath = GetStringParam(p, "source");
            string hintPath = GetStringParam(p, "hint");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(constraintTypeName))
                throw new ArgumentException("constraint_type is required");
            if (string.IsNullOrEmpty(constraintTargetPath))
                throw new ArgumentException("constraint_target is required");

            var root = FindGameObject(targetPath);
            EnsureRigBuilder(root);
            var rigTransform = EnsureRigLayer(root);

            // Get the constraint type
            var constraintType = GetConstraintType(constraintTypeName);
            if (constraintType == null)
                throw new ArgumentException($"Constraint type '{constraintTypeName}' not found. Available: TwoBoneIK, MultiAim, MultiPosition, MultiRotation, ChainIK, DampedTransform, OverrideTransform");

            // Create constraint GameObject under the rig
            var constraintBone = FindGameObject(constraintTargetPath);
            var constraintGo = new GameObject($"{constraintBone.name}_{constraintTypeName}");
            Undo.RegisterCreatedObjectUndo(constraintGo, "Add Rig Constraint");
            constraintGo.transform.SetParent(rigTransform, false);

            var constraint = Undo.AddComponent(constraintGo, constraintType);

            // Try to set constrained object via data property
            try
            {
                var dataProp = constraintType.GetProperty("data");
                if (dataProp != null)
                {
                    var data = dataProp.GetValue(constraint);
                    var dataType = data.GetType();

                    // Set constrained object / tip
                    SetTransformField(dataType, data, "constrainedObject", constraintBone.transform);
                    SetTransformField(dataType, data, "tip", constraintBone.transform);

                    // Set source if provided
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        var sourceGo = FindGameObject(sourcePath);
                        SetTransformField(dataType, data, "target", sourceGo.transform);
                        SetTransformField(dataType, data, "sourceObject", sourceGo.transform);

                        // For weighted source objects list
                        TryAddSourceObject(dataType, data, sourceGo.transform);
                    }

                    // Set hint if provided
                    if (!string.IsNullOrEmpty(hintPath))
                    {
                        var hintGo = FindGameObject(hintPath);
                        SetTransformField(dataType, data, "hint", hintGo.transform);
                    }

                    dataProp.SetValue(constraint, data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not fully configure constraint: {ex.Message}");
            }

            EditorUtility.SetDirty(constraintGo);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "constraint_type", constraintTypeName },
                { "constraint_object", constraintGo.name },
                { "rig", rigTransform.name },
                { "target_bone", constraintBone.name }
            };
        }

        private static void SetTransformField(Type dataType, object data, string fieldName, Transform value)
        {
            var field = dataType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(Transform))
            {
                field.SetValue(data, value);
                return;
            }
            var prop = dataType.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(Transform))
            {
                prop.SetValue(data, value);
            }
        }

        private static void TryAddSourceObject(Type dataType, object data, Transform source)
        {
            // Try to find sourceObjects property for weighted constraints
            var sourceObjectsProp = dataType.GetProperty("sourceObjects", BindingFlags.Instance | BindingFlags.Public);
            if (sourceObjectsProp == null) return;

            try
            {
                var sourceObjects = sourceObjectsProp.GetValue(data);
                var addMethod = sourceObjects.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    // Create WeightedTransform
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var t in assembly.GetTypes())
                        {
                            if (t.Name == "WeightedTransform")
                            {
                                var ctor = t.GetConstructor(new[] { typeof(Transform), typeof(float) });
                                if (ctor != null)
                                {
                                    var wt = ctor.Invoke(new object[] { source, 1f });
                                    addMethod.Invoke(sourceObjects, new[] { wt });
                                    sourceObjectsProp.SetValue(data, sourceObjects);
                                }
                                return;
                            }
                        }
                    }
                }
            }
            catch { /* Best effort */ }
        }

        private static object SetupIK(Dictionary<string, object> p)
        {
            ThrowIfPlaying("setup_ik");

            string targetPath = GetStringParam(p, "target");
            string chain = GetStringParam(p, "chain");
            string tipBonePath = GetStringParam(p, "tip_bone");
            string midBonePath = GetStringParam(p, "mid_bone");
            string rootBonePath = GetStringParam(p, "root_bone");

            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(chain)) throw new ArgumentException("chain is required");
            if (string.IsNullOrEmpty(tipBonePath)) throw new ArgumentException("tip_bone is required");
            if (string.IsNullOrEmpty(midBonePath)) throw new ArgumentException("mid_bone is required");
            if (string.IsNullOrEmpty(rootBonePath)) throw new ArgumentException("root_bone is required");

            var root = FindGameObject(targetPath);
            var tipBone = FindGameObject(tipBonePath);
            var midBone = FindGameObject(midBonePath);
            var rootBone = FindGameObject(rootBonePath);

            EnsureRigBuilder(root);
            var rigTransform = EnsureRigLayer(root);

            // Create target and hint GameObjects
            var ikTargetGo = new GameObject($"{chain}_IK_Target");
            Undo.RegisterCreatedObjectUndo(ikTargetGo, "Create IK Target");
            ikTargetGo.transform.SetParent(rigTransform, false);
            ikTargetGo.transform.position = tipBone.transform.position;
            ikTargetGo.transform.rotation = tipBone.transform.rotation;

            var ikHintGo = new GameObject($"{chain}_IK_Hint");
            Undo.RegisterCreatedObjectUndo(ikHintGo, "Create IK Hint");
            ikHintGo.transform.SetParent(rigTransform, false);
            ikHintGo.transform.position = midBone.transform.position +
                (midBone.transform.position - (tipBone.transform.position + rootBone.transform.position) * 0.5f).normalized * 0.3f;

            // Create constraint GO
            var constraintGo = new GameObject($"{chain}_TwoBoneIK");
            Undo.RegisterCreatedObjectUndo(constraintGo, "Create TwoBoneIK");
            constraintGo.transform.SetParent(rigTransform, false);

            var twoBoneType = GetConstraintType("TwoBoneIK");
            if (twoBoneType == null)
                throw new InvalidOperationException("TwoBoneIKConstraint not found. Install 'com.unity.animation.rigging'.");

            var constraint = Undo.AddComponent(constraintGo, twoBoneType);

            // Configure via data property
            try
            {
                var dataProp = twoBoneType.GetProperty("data");
                if (dataProp != null)
                {
                    var data = dataProp.GetValue(constraint);
                    var dataType = data.GetType();

                    SetTransformField(dataType, data, "root", rootBone.transform);
                    SetTransformField(dataType, data, "mid", midBone.transform);
                    SetTransformField(dataType, data, "tip", tipBone.transform);
                    SetTransformField(dataType, data, "target", ikTargetGo.transform);
                    SetTransformField(dataType, data, "hint", ikHintGo.transform);

                    // Set targetPositionWeight and targetRotationWeight to 1
                    var tpw = dataType.GetField("targetPositionWeight", BindingFlags.Instance | BindingFlags.Public);
                    if (tpw != null) tpw.SetValue(data, 1f);
                    var trw = dataType.GetField("targetRotationWeight", BindingFlags.Instance | BindingFlags.Public);
                    if (trw != null) trw.SetValue(data, 1f);
                    var hw = dataType.GetField("hintWeight", BindingFlags.Instance | BindingFlags.Public);
                    if (hw != null) hw.SetValue(data, 1f);

                    dataProp.SetValue(constraint, data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not fully configure TwoBoneIK: {ex.Message}");
            }

            EditorUtility.SetDirty(constraintGo);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "chain", chain },
                { "constraint", constraintGo.name },
                { "ik_target", ikTargetGo.name },
                { "ik_hint", ikHintGo.name },
                { "root_bone", rootBone.name },
                { "mid_bone", midBone.name },
                { "tip_bone", tipBone.name }
            };
        }

        private static object GetRigInfo(Dictionary<string, object> p)
        {
            string targetPath = GetStringParam(p, "target");
            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var root = FindGameObject(targetPath);

            var rigBuilderType = GetRiggingType(RigBuilderTypeName);
            var rigType = GetRiggingType(RigTypeName);

            var result = new Dictionary<string, object>
            {
                { "gameObject", root.name },
                { "has_animation_rigging", rigBuilderType != null }
            };

            if (rigBuilderType == null)
            {
                result["message"] = "Animation Rigging package not installed";
                return result;
            }

            var rigBuilder = root.GetComponent(rigBuilderType);
            result["has_rig_builder"] = rigBuilder != null;

            if (rigBuilder == null)
            {
                result["rigs"] = new List<object>();
                return result;
            }

            // Enumerate rig layers
            var rigs = new List<object>();

            if (rigType != null)
            {
                var rigComponents = root.GetComponentsInChildren(rigType);
                foreach (var rig in rigComponents)
                {
                    var rigGo = ((Component)rig).gameObject;
                    var rigInfo = new Dictionary<string, object>
                    {
                        { "name", rigGo.name },
                        { "path", GetGameObjectPath(rigGo) }
                    };

                    // Get weight
                    var weightProp = rigType.GetProperty("weight");
                    if (weightProp != null)
                        rigInfo["weight"] = weightProp.GetValue(rig);

                    // Enumerate constraints under this rig
                    var constraints = new List<object>();
                    foreach (Transform child in rigGo.transform)
                    {
                        var components = child.GetComponents<Component>();
                        foreach (var comp in components)
                        {
                            if (comp == null) continue;
                            var compType = comp.GetType();
                            // Check if it implements IRigConstraint or has "Constraint" in name
                            if (compType.Name.Contains("Constraint") && compType.Namespace != null &&
                                compType.Namespace.Contains("Rigging"))
                            {
                                var constraintInfo = new Dictionary<string, object>
                                {
                                    { "name", child.name },
                                    { "type", compType.Name },
                                    { "path", GetGameObjectPath(child.gameObject) }
                                };

                                // Try to read weight
                                var cWeightProp = compType.GetProperty("weight");
                                if (cWeightProp != null)
                                    constraintInfo["weight"] = cWeightProp.GetValue(comp);

                                constraints.Add(constraintInfo);
                            }
                        }
                    }

                    rigInfo["constraints"] = constraints;
                    rigInfo["constraint_count"] = constraints.Count;
                    rigs.Add(rigInfo);
                }
            }

            result["rigs"] = rigs;
            result["rig_count"] = rigs.Count;
            result["success"] = true;

            return result;
        }

        private static object AddRigLayer(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_rig_layer");

            string targetPath = GetStringParam(p, "target");
            string rigName = GetStringParam(p, "rig_name", "Rig");
            float weight = GetFloatParam(p, "weight", 1f);

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var root = FindGameObject(targetPath);
            EnsureRigBuilder(root);

            var rigType = GetRiggingType(RigTypeName);
            if (rigType == null)
                throw new InvalidOperationException("Rig type not found. Animation Rigging package may not be installed.");

            var rigGo = new GameObject(rigName);
            Undo.RegisterCreatedObjectUndo(rigGo, "Add Rig Layer");
            rigGo.transform.SetParent(root.transform, false);

            var rigComponent = Undo.AddComponent(rigGo, rigType);

            // Set weight
            var weightProp = rigType.GetProperty("weight");
            if (weightProp != null && weightProp.CanWrite)
                weightProp.SetValue(rigComponent, weight);

            // Add to RigBuilder layers
            var rigBuilder = root.GetComponent(GetRiggingType(RigBuilderTypeName));
            if (rigBuilder != null)
            {
                var layersProp = rigBuilder.GetType().GetProperty("layers") ??
                                 rigBuilder.GetType().GetProperty("RigLayers");
                if (layersProp != null)
                {
                    try
                    {
                        var layers = layersProp.GetValue(rigBuilder);
                        var addMethod = layers.GetType().GetMethod("Add");
                        if (addMethod != null)
                        {
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                foreach (var t in assembly.GetTypes())
                                {
                                    if (t.Name == "RigLayer")
                                    {
                                        var ctor = t.GetConstructor(new[] { rigType, typeof(bool) });
                                        if (ctor != null)
                                        {
                                            var layer = ctor.Invoke(new object[] { rigComponent, true });
                                            addMethod.Invoke(layers, new[] { layer });
                                        }
                                        else
                                        {
                                            ctor = t.GetConstructor(new[] { rigType });
                                            if (ctor != null)
                                            {
                                                var layer = ctor.Invoke(new object[] { rigComponent });
                                                addMethod.Invoke(layers, new[] { layer });
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not add rig to RigBuilder layers: {ex.Message}");
                    }
                }
            }

            EditorUtility.SetDirty(root);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "rig_name", rigName },
                { "weight", weight },
                { "parent", root.name },
                { "path", GetGameObjectPath(rigGo) }
            };
        }
    }
}
