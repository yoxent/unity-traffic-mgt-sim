using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class SceneViewCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("get_scene_view_camera", GetSceneViewCamera);
            router.Register("set_scene_view_camera", SetSceneViewCamera);
            router.Register("frame_object", FrameObject);
            router.Register("align_scene_view", AlignSceneView);
        }

        private static SceneView GetActiveSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                sv = SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
            if (sv == null)
                throw new InvalidOperationException("No Scene View is open. Open a Scene View window first.");
            return sv;
        }

        private static object GetSceneViewCamera(Dictionary<string, object> p)
        {
            var sv = GetActiveSceneView();
            var cam = sv.camera;

            var euler = sv.rotation.eulerAngles;

            return new Dictionary<string, object>
            {
                { "position", $"{cam.transform.position.x},{cam.transform.position.y},{cam.transform.position.z}" },
                { "rotation", $"{euler.x},{euler.y},{euler.z}" },
                { "pivot", $"{sv.pivot.x},{sv.pivot.y},{sv.pivot.z}" },
                { "size", sv.size },
                { "orthographic", sv.orthographic },
                { "cameraDistance", sv.cameraDistance },
                { "drawMode", sv.cameraMode.drawMode.ToString() },
                { "fieldOfView", cam.fieldOfView },
                { "nearClipPlane", cam.nearClipPlane },
                { "farClipPlane", cam.farClipPlane }
            };
        }

        private static object SetSceneViewCamera(Dictionary<string, object> p)
        {
            var sv = GetActiveSceneView();

            string posStr = GetStringParam(p, "position");
            string rotStr = GetStringParam(p, "rotation");
            string pivotStr = GetStringParam(p, "pivot");
            float size = GetFloatParam(p, "size", -1f);
            string drawMode = GetStringParam(p, "draw_mode");

            var changes = new List<string>();

            if (!string.IsNullOrEmpty(pivotStr))
            {
                sv.pivot = TypeParser.ParseVector3(pivotStr);
                changes.Add("pivot");
            }

            if (!string.IsNullOrEmpty(rotStr))
            {
                var euler = TypeParser.ParseVector3(rotStr);
                sv.rotation = Quaternion.Euler(euler);
                changes.Add("rotation");
            }

            if (!string.IsNullOrEmpty(posStr))
            {
                // Setting position: derive pivot from position and current rotation/size
                var pos = TypeParser.ParseVector3(posStr);
                sv.pivot = pos + sv.rotation * Vector3.forward * sv.cameraDistance;
                changes.Add("position");
            }

            if (size > 0)
            {
                sv.size = size;
                changes.Add("size");
            }

            if (p.ContainsKey("orthographic"))
            {
                sv.orthographic = GetBoolParam(p, "orthographic");
                changes.Add("orthographic");
            }

            if (!string.IsNullOrEmpty(drawMode))
            {
                var mode = ParseDrawMode(drawMode);
                if (mode.HasValue)
                {
                    sv.cameraMode = new SceneView.CameraMode
                    {
                        drawMode = mode.Value,
                        name = mode.Value.ToString(),
                        section = "Shading Mode"
                    };
                    changes.Add("drawMode");
                }
            }

            sv.Repaint();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "changes", changes },
                { "message", $"Scene View camera updated: {string.Join(", ", changes)}" }
            };
        }

        private static DrawCameraMode? ParseDrawMode(string mode)
        {
            switch (mode.ToLower())
            {
                case "textured": return DrawCameraMode.Textured;
                case "wireframe": return DrawCameraMode.Wireframe;
                case "texturedwire": return DrawCameraMode.TexturedWire;
                case "shadedwireframe": return DrawCameraMode.TexturedWire;
                case "shaded": return DrawCameraMode.Textured;
                default:
                    if (Enum.TryParse<DrawCameraMode>(mode, true, out var parsed))
                        return parsed;
                    return null;
            }
        }

        private static object FrameObject(Dictionary<string, object> p)
        {
            string target = GetStringParam(p, "target");
            bool instant = GetBoolParam(p, "instant");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");

            var go = FindGameObject(target);
            var sv = GetActiveSceneView();

            // Calculate bounds including all renderers and colliders
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool hasBounds = false;

            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (hasBounds)
                    bounds.Encapsulate(r.bounds);
                else
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
            }

            var colliders = go.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (hasBounds)
                    bounds.Encapsulate(c.bounds);
                else
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds)
                bounds = new Bounds(go.transform.position, Vector3.one);

            if (instant)
            {
                sv.Frame(bounds, false);
            }
            else
            {
                // Use selection-based framing for smooth animation
                var previousSelection = Selection.activeGameObject;
                Selection.activeGameObject = go;
                sv.FrameSelected();
                // Restore selection if it was different
                if (previousSelection != go)
                    EditorApplication.delayCall += () => Selection.activeGameObject = previousSelection;
            }

            sv.Repaint();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "boundsCenter", $"{bounds.center.x},{bounds.center.y},{bounds.center.z}" },
                { "boundsSize", $"{bounds.size.x},{bounds.size.y},{bounds.size.z}" },
                { "instant", instant }
            };
        }

        private static object AlignSceneView(Dictionary<string, object> p)
        {
            string direction = GetStringParam(p, "direction");

            if (string.IsNullOrEmpty(direction))
                throw new ArgumentException("direction is required");

            var sv = GetActiveSceneView();

            // Default: orthographic for axis-aligned, perspective for "perspective"
            bool ortho;
            if (p.ContainsKey("orthographic"))
                ortho = GetBoolParam(p, "orthographic");
            else
                ortho = direction.ToLower() != "perspective";

            Quaternion rotation;
            switch (direction.ToLower())
            {
                case "top":
                    rotation = Quaternion.Euler(90, 0, 0);
                    break;
                case "bottom":
                    rotation = Quaternion.Euler(-90, 0, 0);
                    break;
                case "front":
                    rotation = Quaternion.Euler(0, 0, 0);
                    break;
                case "back":
                    rotation = Quaternion.Euler(0, 180, 0);
                    break;
                case "left":
                    rotation = Quaternion.Euler(0, 90, 0);
                    break;
                case "right":
                    rotation = Quaternion.Euler(0, -90, 0);
                    break;
                case "perspective":
                    rotation = Quaternion.Euler(30, -45, 0);
                    ortho = false;
                    break;
                default:
                    throw new ArgumentException($"Invalid direction: {direction}. Use: top, bottom, front, back, left, right, perspective");
            }

            sv.orthographic = ortho;
            sv.rotation = rotation;
            sv.Repaint();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "direction", direction },
                { "orthographic", ortho },
                { "rotation", $"{rotation.eulerAngles.x},{rotation.eulerAngles.y},{rotation.eulerAngles.z}" },
                { "message", $"Scene View aligned to {direction} ({(ortho ? "orthographic" : "perspective")})" }
            };
        }
    }
}
