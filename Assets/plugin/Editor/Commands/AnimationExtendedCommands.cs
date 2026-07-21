using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityMcpPro
{
    public class AnimationExtendedCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_blend_tree", CreateBlendTree);
            router.Register("set_blend_tree_motion", SetBlendTreeMotion);
            router.Register("add_animation_layer", AddAnimationLayer);
            router.Register("set_avatar_mask", SetAvatarMask);
            router.Register("get_animator_info", GetAnimatorInfo);
        }

        private static object CreateBlendTree(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_blend_tree");
            string controllerPath = GetStringParam(p, "controller_path");
            string stateName = GetStringParam(p, "state_name");
            string blendTypeStr = GetStringParam(p, "blend_type", "Simple1D");
            string parameter = GetStringParam(p, "parameter");

            if (string.IsNullOrEmpty(controllerPath))
                throw new ArgumentException("controller_path is required");
            if (string.IsNullOrEmpty(stateName))
                throw new ArgumentException("state_name is required");
            if (string.IsNullOrEmpty(parameter))
                throw new ArgumentException("parameter is required");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                throw new ArgumentException($"Animator controller not found at: {controllerPath}");

            BlendTreeType blendType;
            switch (blendTypeStr)
            {
                case "SimpleDirectional2D": blendType = BlendTreeType.SimpleDirectional2D; break;
                case "FreeformDirectional2D": blendType = BlendTreeType.FreeformDirectional2D; break;
                case "FreeformCartesian2D": blendType = BlendTreeType.FreeformCartesian2D; break;
                case "Simple1D":
                default: blendType = BlendTreeType.Simple1D; break;
            }

            // Find the state across all layers
            AnimatorState targetState = null;
            int foundLayer = -1;
            for (int li = 0; li < controller.layers.Length; li++)
            {
                var stateMachine = controller.layers[li].stateMachine;
                foreach (var cs in stateMachine.states)
                {
                    if (cs.state.name == stateName)
                    {
                        targetState = cs.state;
                        foundLayer = li;
                        break;
                    }
                }
                if (targetState != null) break;
            }

            if (targetState == null)
                throw new ArgumentException($"State not found: {stateName}");

            // Ensure the parameter exists on the controller
            bool paramExists = false;
            foreach (var param in controller.parameters)
            {
                if (param.name == parameter)
                {
                    paramExists = true;
                    break;
                }
            }
            if (!paramExists)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            }

            // Create blend tree and assign to state
            var blendTree = new BlendTree();
            blendTree.name = stateName + " BlendTree";
            blendTree.blendType = blendType;
            blendTree.blendParameter = parameter;

            // For 2D blend trees, set the second parameter to the same by default
            if (blendType != BlendTreeType.Simple1D)
            {
                blendTree.blendParameterY = parameter + "Y";
                bool paramYExists = false;
                foreach (var param in controller.parameters)
                {
                    if (param.name == parameter + "Y")
                    {
                        paramYExists = true;
                        break;
                    }
                }
                if (!paramYExists)
                {
                    controller.AddParameter(parameter + "Y", AnimatorControllerParameterType.Float);
                }
            }

            // Add blend tree as sub-asset of the controller
            AssetDatabase.AddObjectToAsset(blendTree, controllerPath);
            targetState.motion = blendTree;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "controller", controller.name },
                { "state", stateName },
                { "blendType", blendTypeStr },
                { "parameter", parameter },
                { "layer", foundLayer }
            };
        }

        private static object SetBlendTreeMotion(Dictionary<string, object> p)
        {
            ThrowIfPlaying("set_blend_tree_motion");
            string controllerPath = GetStringParam(p, "controller_path");
            string stateName = GetStringParam(p, "state_name");
            string clipPath = GetStringParam(p, "clip_path");
            float threshold = GetFloatParam(p, "threshold");
            string positionStr = GetStringParam(p, "position");

            if (string.IsNullOrEmpty(controllerPath))
                throw new ArgumentException("controller_path is required");
            if (string.IsNullOrEmpty(stateName))
                throw new ArgumentException("state_name is required");
            if (string.IsNullOrEmpty(clipPath))
                throw new ArgumentException("clip_path is required");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                throw new ArgumentException($"Animator controller not found at: {controllerPath}");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                throw new ArgumentException($"Animation clip not found at: {clipPath}");

            // Find the state and its blend tree
            BlendTree blendTree = null;
            foreach (var layer in controller.layers)
            {
                foreach (var cs in layer.stateMachine.states)
                {
                    if (cs.state.name == stateName)
                    {
                        blendTree = cs.state.motion as BlendTree;
                        break;
                    }
                }
                if (blendTree != null) break;
            }

            if (blendTree == null)
                throw new ArgumentException($"BlendTree not found in state: {stateName}. Create one first with create_blend_tree.");

            if (!string.IsNullOrEmpty(positionStr) && blendTree.blendType != BlendTreeType.Simple1D)
            {
                // 2D blend tree - parse position
                var parts = positionStr.Split(',');
                if (parts.Length != 2)
                    throw new ArgumentException("position must be 'x,y' format");

                float px = float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                float py = float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                blendTree.AddChild(clip, new Vector2(px, py));
            }
            else
            {
                blendTree.AddChild(clip, threshold);
            }

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "state", stateName },
                { "clip", clip.name },
                { "threshold", threshold },
                { "childCount", blendTree.children.Length }
            };
        }

        private static object AddAnimationLayer(Dictionary<string, object> p)
        {
            ThrowIfPlaying("add_animation_layer");
            string controllerPath = GetStringParam(p, "controller_path");
            string layerName = GetStringParam(p, "layer_name");
            float weight = GetFloatParam(p, "weight", 1f);
            string blendingStr = GetStringParam(p, "blending", "Override");
            string avatarMaskPath = GetStringParam(p, "avatar_mask_path");

            if (string.IsNullOrEmpty(controllerPath))
                throw new ArgumentException("controller_path is required");
            if (string.IsNullOrEmpty(layerName))
                throw new ArgumentException("layer_name is required");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                throw new ArgumentException($"Animator controller not found at: {controllerPath}");

            AnimatorLayerBlendingMode blendingMode;
            switch (blendingStr.ToLower())
            {
                case "additive": blendingMode = AnimatorLayerBlendingMode.Additive; break;
                case "override":
                default: blendingMode = AnimatorLayerBlendingMode.Override; break;
            }

            var newLayer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = weight,
                blendingMode = blendingMode,
                stateMachine = new AnimatorStateMachine()
            };
            newLayer.stateMachine.name = layerName;
            newLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

            if (!string.IsNullOrEmpty(avatarMaskPath))
            {
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(avatarMaskPath);
                if (mask == null)
                    throw new ArgumentException($"AvatarMask not found at: {avatarMaskPath}");
                newLayer.avatarMask = mask;
            }

            // Add state machine as sub-asset
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controllerPath);

            controller.AddLayer(newLayer);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "controller", controller.name },
                { "layer", layerName },
                { "layerIndex", controller.layers.Length - 1 },
                { "weight", weight },
                { "blending", blendingStr },
                { "hasAvatarMask", !string.IsNullOrEmpty(avatarMaskPath) }
            };
        }

        private static object SetAvatarMask(Dictionary<string, object> p)
        {
            ThrowIfPlaying("set_avatar_mask");
            string path = GetStringParam(p, "path");
            var bodyParts = GetDictParam(p, "body_parts");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");
            if (bodyParts == null || bodyParts.Count == 0)
                throw new ArgumentException("body_parts is required");

            if (!path.EndsWith(".mask"))
                path += ".mask";

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var mask = new AvatarMask();

            // Map of body part names to AvatarMaskBodyPart enum values
            var bodyPartMap = new Dictionary<string, AvatarMaskBodyPart>(StringComparer.OrdinalIgnoreCase)
            {
                { "Root", AvatarMaskBodyPart.Root },
                { "Body", AvatarMaskBodyPart.Body },
                { "Head", AvatarMaskBodyPart.Head },
                { "LeftLeg", AvatarMaskBodyPart.LeftLeg },
                { "RightLeg", AvatarMaskBodyPart.RightLeg },
                { "LeftArm", AvatarMaskBodyPart.LeftArm },
                { "RightArm", AvatarMaskBodyPart.RightArm },
                { "LeftFingers", AvatarMaskBodyPart.LeftFingers },
                { "RightFingers", AvatarMaskBodyPart.RightFingers },
                { "LeftFootIK", AvatarMaskBodyPart.LeftFootIK },
                { "RightFootIK", AvatarMaskBodyPart.RightFootIK },
                { "LeftHandIK", AvatarMaskBodyPart.LeftHandIK },
                { "RightHandIK", AvatarMaskBodyPart.RightHandIK }
            };

            // Default all parts to active
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            }

            // Apply user-specified body parts
            var appliedParts = new List<string>();
            foreach (var kvp in bodyParts)
            {
                if (bodyPartMap.TryGetValue(kvp.Key, out var part))
                {
                    bool active = false;
                    if (kvp.Value is bool b) active = b;
                    else if (kvp.Value != null) bool.TryParse(kvp.Value.ToString(), out active);

                    mask.SetHumanoidBodyPartActive(part, active);
                    appliedParts.Add($"{kvp.Key}={active}");
                }
                else
                {
                    throw new ArgumentException($"Unknown body part: {kvp.Key}. Available: Root, Body, Head, LeftLeg, RightLeg, LeftArm, RightArm, LeftFingers, RightFingers, LeftFootIK, RightFootIK, LeftHandIK, RightHandIK");
                }
            }

            AssetDatabase.CreateAsset(mask, path);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "bodyParts", appliedParts }
            };
        }

        private static object GetAnimatorInfo(Dictionary<string, object> p)
        {
            string controllerPath = GetStringParam(p, "controller_path");

            if (string.IsNullOrEmpty(controllerPath))
                throw new ArgumentException("controller_path is required");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                throw new ArgumentException($"Animator controller not found at: {controllerPath}");

            // Parameters
            var parameters = new List<object>();
            foreach (var param in controller.parameters)
            {
                var paramInfo = new Dictionary<string, object>
                {
                    { "name", param.name },
                    { "type", param.type.ToString() }
                };
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        paramInfo["defaultValue"] = param.defaultFloat;
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramInfo["defaultValue"] = param.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        paramInfo["defaultValue"] = param.defaultBool;
                        break;
                }
                parameters.Add(paramInfo);
            }

            // Layers
            var layers = new List<object>();
            for (int li = 0; li < controller.layers.Length; li++)
            {
                var layer = controller.layers[li];
                var stateMachine = layer.stateMachine;

                // States
                var states = new List<object>();
                foreach (var cs in stateMachine.states)
                {
                    var stateInfo = new Dictionary<string, object>
                    {
                        { "name", cs.state.name },
                        { "speed", cs.state.speed },
                        { "tag", cs.state.tag },
                        { "hasMotion", cs.state.motion != null },
                        { "isDefault", stateMachine.defaultState == cs.state }
                    };

                    if (cs.state.motion != null)
                    {
                        stateInfo["motionName"] = cs.state.motion.name;
                        stateInfo["motionType"] = cs.state.motion is BlendTree ? "BlendTree" : "AnimationClip";

                        // Blend tree details
                        if (cs.state.motion is BlendTree bt)
                        {
                            var btChildren = new List<object>();
                            foreach (var child in bt.children)
                            {
                                btChildren.Add(new Dictionary<string, object>
                                {
                                    { "motion", child.motion != null ? child.motion.name : "(none)" },
                                    { "threshold", child.threshold },
                                    { "position", $"{child.position.x},{child.position.y}" }
                                });
                            }
                            stateInfo["blendTree"] = new Dictionary<string, object>
                            {
                                { "blendType", bt.blendType.ToString() },
                                { "blendParameter", bt.blendParameter },
                                { "blendParameterY", bt.blendParameterY },
                                { "children", btChildren }
                            };
                        }
                    }

                    // Transitions from this state
                    var transitions = new List<object>();
                    foreach (var t in cs.state.transitions)
                    {
                        var transInfo = new Dictionary<string, object>
                        {
                            { "destinationState", t.destinationState != null ? t.destinationState.name : "(exit)" },
                            { "hasExitTime", t.hasExitTime },
                            { "exitTime", t.exitTime },
                            { "duration", t.duration },
                            { "hasFixedDuration", t.hasFixedDuration }
                        };

                        var conditions = new List<object>();
                        foreach (var c in t.conditions)
                        {
                            conditions.Add(new Dictionary<string, object>
                            {
                                { "parameter", c.parameter },
                                { "mode", c.mode.ToString() },
                                { "threshold", c.threshold }
                            });
                        }
                        transInfo["conditions"] = conditions;
                        transitions.Add(transInfo);
                    }
                    stateInfo["transitions"] = transitions;

                    states.Add(stateInfo);
                }

                var layerInfo = new Dictionary<string, object>
                {
                    { "name", layer.name },
                    { "index", li },
                    { "weight", layer.defaultWeight },
                    { "blendingMode", layer.blendingMode.ToString() },
                    { "hasAvatarMask", layer.avatarMask != null },
                    { "avatarMask", layer.avatarMask != null ? layer.avatarMask.name : null },
                    { "states", states },
                    { "stateCount", stateMachine.states.Length }
                };

                layers.Add(layerInfo);
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", controller.name },
                { "path", controllerPath },
                { "parameters", parameters },
                { "layers", layers },
                { "layerCount", controller.layers.Length },
                { "parameterCount", controller.parameters.Length }
            };
        }
    }
}
