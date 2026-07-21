using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcpPro
{
    public class CameraCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("add_cinemachine_camera", AddCinemachineCamera);
            router.Register("set_cinemachine_body", SetCinemachineBody);
            router.Register("set_cinemachine_aim", SetCinemachineAim);
            router.Register("get_cinemachine_info", GetCinemachineInfo);
            router.Register("add_camera_path", AddCameraPath);
            router.Register("setup_camera_follow", SetupCameraFollow);
        }

        // --- Reflection helpers for Cinemachine ---

        private static Type FindCinemachineType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        private static Type GetVirtualCameraType()
        {
            // Try CM3 first (Cinemachine 3.x uses "CinemachineCamera")
            var cm3 = FindCinemachineType("Unity.Cinemachine.CinemachineCamera");
            if (cm3 != null) return cm3;

            // Try CM2 (Cinemachine 2.x uses "CinemachineVirtualCamera")
            var cm2 = FindCinemachineType("Cinemachine.CinemachineVirtualCamera");
            if (cm2 != null) return cm2;

            return null;
        }

        private static bool IsCinemachine3()
        {
            return FindCinemachineType("Unity.Cinemachine.CinemachineCamera") != null;
        }

        private static void RequireCinemachine()
        {
            if (GetVirtualCameraType() == null)
                throw new InvalidOperationException(
                    "Cinemachine package not installed. Add it via Window > Package Manager > Unity Registry > Cinemachine.");
        }

        private static void SetPropertyViaReflection(object obj, string propName, object value)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }
            var field = type.GetField(propName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }
        }

        private static object GetPropertyViaReflection(object obj, string propName)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(obj);
            var field = type.GetField(propName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(obj);
            return null;
        }

        // --- Tool handlers ---

        private static object AddCinemachineCamera(Dictionary<string, object> p)
        {
            RequireCinemachine();

            string name = GetStringParam(p, "name", "Virtual Camera");
            string posStr = GetStringParam(p, "position");
            string followPath = GetStringParam(p, "follow");
            string lookAtPath = GetStringParam(p, "look_at");

            var vcamType = GetVirtualCameraType();
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "MCP: Add Cinemachine Camera");

            var vcam = go.AddComponent(vcamType);

            if (!string.IsNullOrEmpty(posStr))
                go.transform.position = TypeParser.ParseVector3(posStr);

            if (!string.IsNullOrEmpty(followPath))
            {
                var followGo = FindGameObject(followPath);
                SetPropertyViaReflection(vcam, "Follow", followGo.transform);
                // CM3 uses "Target" with sub-properties, try both
                try
                {
                    var trackingTarget = vcamType.GetProperty("TrackingTarget");
                    if (trackingTarget != null)
                        trackingTarget.SetValue(vcam, followGo.transform);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(lookAtPath))
            {
                var lookGo = FindGameObject(lookAtPath);
                SetPropertyViaReflection(vcam, "LookAt", lookGo.transform);
            }

            // Ensure a CinemachineBrain exists on the main camera
            EnsureBrainExists();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "path", GetGameObjectPath(go) },
                { "cinemachineVersion", IsCinemachine3() ? "3.x" : "2.x" },
                { "follow", followPath ?? "none" },
                { "lookAt", lookAtPath ?? "none" }
            };
        }

        private static void EnsureBrainExists()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            Type brainType = FindCinemachineType("Unity.Cinemachine.CinemachineBrain")
                ?? FindCinemachineType("Cinemachine.CinemachineBrain");

            if (brainType == null) return;

            if (mainCam.GetComponent(brainType) == null)
            {
                Undo.AddComponent(mainCam.gameObject, brainType);
            }
        }

        private static object SetCinemachineBody(Dictionary<string, object> p)
        {
            RequireCinemachine();

            string targetPath = GetStringParam(p, "target");
            string bodyType = GetStringParam(p, "body_type");
            string offsetStr = GetStringParam(p, "follow_offset");
            float damping = GetFloatParam(p, "damping", -1f);

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(bodyType))
                throw new ArgumentException("body_type is required");

            var go = FindGameObject(targetPath);
            var vcamType = GetVirtualCameraType();
            var vcam = go.GetComponent(vcamType);
            if (vcam == null)
                throw new ArgumentException($"No CinemachineVirtualCamera found on {targetPath}");

            RecordUndo(vcam as UnityEngine.Object, "Set Cinemachine Body");

            if (IsCinemachine3())
            {
                // CM3: Body behaviors are added as components on the same GameObject
                SetCM3Body(go, vcam, bodyType, offsetStr, damping);
            }
            else
            {
                // CM2: Use reflection to set body via pipeline
                SetCM2Body(vcam, bodyType, offsetStr, damping);
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", GetGameObjectPath(go) },
                { "bodyType", bodyType },
                { "followOffset", offsetStr ?? "default" }
            };
        }

        private static void SetCM3Body(GameObject go, Component vcam, string bodyType, string offsetStr, float damping)
        {
            // CM3 uses component-based pipeline
            string cm3TypeName = null;
            switch (bodyType.ToLower())
            {
                case "transposer":
                    cm3TypeName = "Unity.Cinemachine.CinemachineFollow";
                    break;
                case "framingtransposer":
                    cm3TypeName = "Unity.Cinemachine.CinemachinePositionComposer";
                    break;
                case "orbitaltransposer":
                    cm3TypeName = "Unity.Cinemachine.CinemachineOrbitalFollow";
                    break;
                case "hardlocktotarget":
                    cm3TypeName = "Unity.Cinemachine.CinemachineHardLockToTarget";
                    break;
                default:
                    throw new ArgumentException($"Unknown body type: {bodyType}. Use: Transposer, FramingTransposer, OrbitalTransposer, HardLockToTarget");
            }

            var compType = FindCinemachineType(cm3TypeName);
            if (compType == null)
                throw new ArgumentException($"Cinemachine component type not found: {cm3TypeName}");

            var existing = go.GetComponent(compType);
            if (existing == null)
                existing = Undo.AddComponent(go, compType);

            if (!string.IsNullOrEmpty(offsetStr))
            {
                var offset = TypeParser.ParseVector3(offsetStr);
                SetPropertyViaReflection(existing, "FollowOffset", offset);
                SetPropertyViaReflection(existing, "TrackerSettings", null); // fallback
            }

            if (damping >= 0)
            {
                SetPropertyViaReflection(existing, "Damping", damping);
            }
        }

        private static void SetCM2Body(object vcam, string bodyType, string offsetStr, float damping)
        {
            // CM2: GetCinemachineComponent with stage enum
            var stageType = FindCinemachineType("Cinemachine.CinemachineCore+Stage");
            if (stageType == null) return;

            var bodyStage = Enum.Parse(stageType, "Body");

            string cm2TypeName = null;
            switch (bodyType.ToLower())
            {
                case "transposer":
                    cm2TypeName = "Cinemachine.CinemachineTransposer";
                    break;
                case "framingtransposer":
                    cm2TypeName = "Cinemachine.CinemachineFramingTransposer";
                    break;
                case "orbitaltransposer":
                    cm2TypeName = "Cinemachine.CinemachineOrbitalTransposer";
                    break;
                case "hardlocktotarget":
                    cm2TypeName = "Cinemachine.CinemachineHardLockToTarget";
                    break;
                default:
                    throw new ArgumentException($"Unknown body type: {bodyType}. Use: Transposer, FramingTransposer, OrbitalTransposer, HardLockToTarget");
            }

            // Use AddCinemachineComponent via reflection
            var vcamType = vcam.GetType();
            var addMethod = vcamType.GetMethod("AddCinemachineComponent",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

            var compType = FindCinemachineType(cm2TypeName);
            if (compType == null)
                throw new ArgumentException($"Cinemachine component type not found: {cm2TypeName}");

            // Use generic AddCinemachineComponent<T>
            var genericAdd = vcamType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "AddCinemachineComponent" && m.IsGenericMethod);

            if (genericAdd != null)
            {
                var specificAdd = genericAdd.MakeGenericMethod(compType);
                var body = specificAdd.Invoke(vcam, null);

                if (body != null)
                {
                    if (!string.IsNullOrEmpty(offsetStr))
                    {
                        var offset = TypeParser.ParseVector3(offsetStr);
                        SetPropertyViaReflection(body, "m_FollowOffset", offset);
                    }
                    if (damping >= 0)
                    {
                        SetPropertyViaReflection(body, "m_XDamping", damping);
                        SetPropertyViaReflection(body, "m_YDamping", damping);
                        SetPropertyViaReflection(body, "m_ZDamping", damping);
                    }
                }
            }
        }

        private static object SetCinemachineAim(Dictionary<string, object> p)
        {
            RequireCinemachine();

            string targetPath = GetStringParam(p, "target");
            string aimType = GetStringParam(p, "aim_type");
            float deadZoneWidth = GetFloatParam(p, "dead_zone_width", -1f);
            float deadZoneHeight = GetFloatParam(p, "dead_zone_height", -1f);

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(aimType))
                throw new ArgumentException("aim_type is required");

            var go = FindGameObject(targetPath);
            var vcamType = GetVirtualCameraType();
            var vcam = go.GetComponent(vcamType);
            if (vcam == null)
                throw new ArgumentException($"No CinemachineVirtualCamera found on {targetPath}");

            RecordUndo(vcam as UnityEngine.Object, "Set Cinemachine Aim");

            if (IsCinemachine3())
            {
                SetCM3Aim(go, aimType, deadZoneWidth, deadZoneHeight);
            }
            else
            {
                SetCM2Aim(vcam, aimType, deadZoneWidth, deadZoneHeight);
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", GetGameObjectPath(go) },
                { "aimType", aimType }
            };
        }

        private static void SetCM3Aim(GameObject go, string aimType, float deadZoneWidth, float deadZoneHeight)
        {
            string cm3TypeName = null;
            switch (aimType.ToLower())
            {
                case "composer":
                    cm3TypeName = "Unity.Cinemachine.CinemachineRotationComposer";
                    break;
                case "hardlookat":
                    cm3TypeName = "Unity.Cinemachine.CinemachineHardLookAt";
                    break;
                case "groupcomposer":
                    cm3TypeName = "Unity.Cinemachine.CinemachineGroupFraming";
                    break;
                case "pov":
                    cm3TypeName = "Unity.Cinemachine.CinemachinePanTilt";
                    break;
                default:
                    throw new ArgumentException($"Unknown aim type: {aimType}. Use: Composer, HardLookAt, GroupComposer, POV");
            }

            var compType = FindCinemachineType(cm3TypeName);
            if (compType == null)
                throw new ArgumentException($"Cinemachine component type not found: {cm3TypeName}");

            var existing = go.GetComponent(compType);
            if (existing == null)
                existing = Undo.AddComponent(go, compType);

            if (deadZoneWidth >= 0)
                SetPropertyViaReflection(existing, "DeadZoneWidth", deadZoneWidth);
            if (deadZoneHeight >= 0)
                SetPropertyViaReflection(existing, "DeadZoneHeight", deadZoneHeight);
        }

        private static void SetCM2Aim(object vcam, string aimType, float deadZoneWidth, float deadZoneHeight)
        {
            string cm2TypeName = null;
            switch (aimType.ToLower())
            {
                case "composer":
                    cm2TypeName = "Cinemachine.CinemachineComposer";
                    break;
                case "hardlookat":
                    cm2TypeName = "Cinemachine.CinemachineHardLookAt";
                    break;
                case "groupcomposer":
                    cm2TypeName = "Cinemachine.CinemachineGroupComposer";
                    break;
                case "pov":
                    cm2TypeName = "Cinemachine.CinemachinePOV";
                    break;
                default:
                    throw new ArgumentException($"Unknown aim type: {aimType}. Use: Composer, HardLookAt, GroupComposer, POV");
            }

            var compType = FindCinemachineType(cm2TypeName);
            if (compType == null)
                throw new ArgumentException($"Cinemachine component type not found: {cm2TypeName}");

            var vcamConcrete = vcam.GetType();
            var genericAdd = vcamConcrete.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "AddCinemachineComponent" && m.IsGenericMethod);

            if (genericAdd != null)
            {
                var specificAdd = genericAdd.MakeGenericMethod(compType);
                var aim = specificAdd.Invoke(vcam, null);

                if (aim != null)
                {
                    if (deadZoneWidth >= 0)
                        SetPropertyViaReflection(aim, "m_DeadZoneWidth", deadZoneWidth);
                    if (deadZoneHeight >= 0)
                        SetPropertyViaReflection(aim, "m_DeadZoneHeight", deadZoneHeight);
                }
            }
        }

        private static object GetCinemachineInfo(Dictionary<string, object> p)
        {
            var vcamType = GetVirtualCameraType();
            if (vcamType == null)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "cinemachineInstalled", false },
                    { "message", "Cinemachine package not installed." },
                    { "cameras", new List<object>() }
                };
            }

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var cameras = new List<object>();

            foreach (var root in rootObjects)
            {
                foreach (var vcam in root.GetComponentsInChildren(vcamType, true))
                {
                    var info = new Dictionary<string, object>
                    {
                        { "name", vcam.gameObject.name },
                        { "path", GetGameObjectPath(vcam.gameObject) },
                        { "enabled", (vcam as Behaviour)?.enabled ?? false },
                        { "position", vcam.transform.position.ToString() },
                        { "rotation", vcam.transform.eulerAngles.ToString() }
                    };

                    // Get Follow and LookAt targets
                    var follow = GetPropertyViaReflection(vcam, "Follow");
                    var lookAt = GetPropertyViaReflection(vcam, "LookAt");
                    info["follow"] = follow != null ? (follow as Transform)?.gameObject?.name : "none";
                    info["lookAt"] = lookAt != null ? (lookAt as Transform)?.gameObject?.name : "none";

                    // Get priority
                    var priority = GetPropertyViaReflection(vcam, "Priority");
                    if (priority != null) info["priority"] = priority;

                    // List pipeline components (CM3 style)
                    var components = vcam.gameObject.GetComponents<Component>();
                    var pipelineComponents = new List<string>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        string typeName = comp.GetType().FullName ?? "";
                        if (typeName.Contains("Cinemachine") && comp != vcam)
                            pipelineComponents.Add(comp.GetType().Name);
                    }
                    if (pipelineComponents.Count > 0)
                        info["pipelineComponents"] = pipelineComponents;

                    cameras.Add(info);
                }
            }

            // Check for CinemachineBrain
            bool hasBrain = false;
            var brainType = FindCinemachineType("Unity.Cinemachine.CinemachineBrain")
                ?? FindCinemachineType("Cinemachine.CinemachineBrain");
            if (brainType != null)
            {
                foreach (var root in rootObjects)
                {
                    if (root.GetComponentInChildren(brainType, true) != null)
                    {
                        hasBrain = true;
                        break;
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "cinemachineInstalled", true },
                { "cinemachineVersion", IsCinemachine3() ? "3.x" : "2.x" },
                { "hasBrain", hasBrain },
                { "cameraCount", cameras.Count },
                { "cameras", cameras }
            };
        }

        private static object AddCameraPath(Dictionary<string, object> p)
        {
            RequireCinemachine();

            string name = GetStringParam(p, "name", "Dolly Track");
            bool closed = GetBoolParam(p, "closed", false);
            var waypointStrs = GetStringListParam(p, "waypoints");

            if (waypointStrs == null || waypointStrs.Length == 0)
                throw new ArgumentException("waypoints array is required (at least 2 points)");

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "MCP: Add Camera Path");

            if (IsCinemachine3())
            {
                // CM3: Use CinemachineSplineDolly + SplineContainer
                var splineContainerType = FindCinemachineType("UnityEngine.Splines.SplineContainer");
                if (splineContainerType != null)
                {
                    var container = go.AddComponent(splineContainerType);
                    // Spline API requires more complex setup; create points via reflection
                    try
                    {
                        var splineProp = splineContainerType.GetProperty("Spline");
                        if (splineProp != null)
                        {
                            var spline = splineProp.GetValue(container);
                            var clearMethod = spline.GetType().GetMethod("Clear");
                            clearMethod?.Invoke(spline, null);

                            var addMethod = spline.GetType().GetMethod("Add",
                                BindingFlags.Public | BindingFlags.Instance,
                                null, new[] { typeof(float3Surrogate) }, null);

                            // Fallback: use positions directly if API not accessible
                            // CM3 splines are complex; set positions on child transforms as fallback
                        }
                    }
                    catch { }
                }

                // Fallback: create waypoint child objects for visual reference
                for (int i = 0; i < waypointStrs.Length; i++)
                {
                    var wpGo = new GameObject($"Waypoint {i}");
                    wpGo.transform.SetParent(go.transform);
                    wpGo.transform.position = TypeParser.ParseVector3(waypointStrs[i]);
                    Undo.RegisterCreatedObjectUndo(wpGo, "MCP: Add Waypoint");
                }
            }
            else
            {
                // CM2: CinemachineSmoothPath
                var pathType = FindCinemachineType("Cinemachine.CinemachineSmoothPath");
                if (pathType == null)
                    pathType = FindCinemachineType("Cinemachine.CinemachinePath");

                if (pathType != null)
                {
                    var path = go.AddComponent(pathType);

                    // Set waypoints via reflection
                    var waypointsField = pathType.GetField("m_Waypoints",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (waypointsField != null)
                    {
                        // CinemachineSmoothPath.Waypoint has 'position' field
                        var waypointType = waypointsField.FieldType.GetElementType();
                        if (waypointType != null)
                        {
                            var wpArray = Array.CreateInstance(waypointType, waypointStrs.Length);
                            for (int i = 0; i < waypointStrs.Length; i++)
                            {
                                var wp = Activator.CreateInstance(waypointType);
                                var posField = waypointType.GetField("position",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (posField != null)
                                    posField.SetValue(wp, TypeParser.ParseVector3(waypointStrs[i]));
                                wpArray.SetValue(wp, i);
                            }
                            waypointsField.SetValue(path, wpArray);
                        }
                    }

                    // Set looped
                    var loopedProp = pathType.GetProperty("Looped",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (loopedProp != null)
                        loopedProp.SetValue(path, closed);
                    else
                    {
                        var loopedField = pathType.GetField("m_Looped",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (loopedField != null)
                            loopedField.SetValue(path, closed);
                    }
                }
                else
                {
                    // Fallback: create waypoint child objects
                    for (int i = 0; i < waypointStrs.Length; i++)
                    {
                        var wpGo = new GameObject($"Waypoint {i}");
                        wpGo.transform.SetParent(go.transform);
                        wpGo.transform.position = TypeParser.ParseVector3(waypointStrs[i]);
                        Undo.RegisterCreatedObjectUndo(wpGo, "MCP: Add Waypoint");
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "path", GetGameObjectPath(go) },
                { "waypointCount", waypointStrs.Length },
                { "closed", closed }
            };
        }

        // Dummy type for spline reflection (never instantiated)
        private struct float3Surrogate { }

        private static object SetupCameraFollow(Dictionary<string, object> p)
        {
            RequireCinemachine();

            string followTargetPath = GetStringParam(p, "follow_target");
            string offsetStr = GetStringParam(p, "offset", "0,5,-10");
            float damping = GetFloatParam(p, "damping", 1f);

            if (string.IsNullOrEmpty(followTargetPath))
                throw new ArgumentException("follow_target is required");

            var followTarget = FindGameObject(followTargetPath);

            // Create virtual camera
            var vcamType = GetVirtualCameraType();
            var go = new GameObject("Follow Camera");
            Undo.RegisterCreatedObjectUndo(go, "MCP: Setup Camera Follow");

            var vcam = go.AddComponent(vcamType);

            // Set follow and look at
            SetPropertyViaReflection(vcam, "Follow", followTarget.transform);
            SetPropertyViaReflection(vcam, "LookAt", followTarget.transform);

            // CM3 tracking target
            try
            {
                var trackingTarget = vcamType.GetProperty("TrackingTarget");
                if (trackingTarget != null)
                    trackingTarget.SetValue(vcam, followTarget.transform);
            }
            catch { }

            // Position the camera at offset from target
            var offset = TypeParser.ParseVector3(offsetStr);
            go.transform.position = followTarget.transform.position + offset;

            // Set up body (Transposer / CinemachineFollow)
            if (IsCinemachine3())
            {
                var followCompType = FindCinemachineType("Unity.Cinemachine.CinemachineFollow");
                if (followCompType != null)
                {
                    var followComp = go.AddComponent(followCompType);
                    SetPropertyViaReflection(followComp, "FollowOffset", offset);
                    SetPropertyViaReflection(followComp, "Damping", new Vector3(damping, damping, damping));
                }

                var aimCompType = FindCinemachineType("Unity.Cinemachine.CinemachineRotationComposer");
                if (aimCompType != null)
                {
                    go.AddComponent(aimCompType);
                }
            }
            else
            {
                // CM2: AddCinemachineComponent<CinemachineTransposer>
                var transposerType = FindCinemachineType("Cinemachine.CinemachineTransposer");
                if (transposerType != null)
                {
                    var genericAdd = vcamType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "AddCinemachineComponent" && m.IsGenericMethod);
                    if (genericAdd != null)
                    {
                        var specificAdd = genericAdd.MakeGenericMethod(transposerType);
                        var transposer = specificAdd.Invoke(vcam, null);
                        if (transposer != null)
                        {
                            SetPropertyViaReflection(transposer, "m_FollowOffset", offset);
                            SetPropertyViaReflection(transposer, "m_XDamping", damping);
                            SetPropertyViaReflection(transposer, "m_YDamping", damping);
                            SetPropertyViaReflection(transposer, "m_ZDamping", damping);
                        }
                    }
                }

                // Add Composer for aim
                var composerType = FindCinemachineType("Cinemachine.CinemachineComposer");
                if (composerType != null)
                {
                    var genericAdd = vcamType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "AddCinemachineComponent" && m.IsGenericMethod);
                    if (genericAdd != null)
                    {
                        var specificAdd = genericAdd.MakeGenericMethod(composerType);
                        specificAdd.Invoke(vcam, null);
                    }
                }
            }

            // Ensure brain exists
            EnsureBrainExists();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "path", GetGameObjectPath(go) },
                { "followTarget", GetGameObjectPath(followTarget) },
                { "offset", offsetStr },
                { "damping", damping },
                { "cinemachineVersion", IsCinemachine3() ? "3.x" : "2.x" }
            };
        }
    }
}
