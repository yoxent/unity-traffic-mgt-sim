using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class SplineCommands : BaseCommand
    {
        private static Type _splineContainerType;
        private static Type _splineType;
        private static Type _bezierKnotType;
        private static Type _float3Type;
        private static Type _quaternionMathType;
        private static Type _splineExtrudeType;
        private static bool _splinePackageChecked;
        private static bool _splinePackageAvailable;

        public static void Register(CommandRouter router)
        {
            router.Register("create_spline", CreateSpline);
            router.Register("add_spline_knot", AddSplineKnot);
            router.Register("set_spline_knot", SetSplineKnot);
            router.Register("get_spline_info", GetSplineInfo);
            router.Register("extrude_spline_mesh", ExtrudeSplineMesh);
        }

        private static bool EnsureSplinePackage()
        {
            if (_splinePackageChecked) return _splinePackageAvailable;

            _splinePackageChecked = true;

            _splineContainerType = FindType("UnityEngine.Splines.SplineContainer");
            _splineType = FindType("UnityEngine.Splines.Spline");
            _bezierKnotType = FindType("UnityEngine.Splines.BezierKnot");
            _float3Type = FindType("Unity.Mathematics.float3");
            _quaternionMathType = FindType("Unity.Mathematics.quaternion");
            _splineExtrudeType = FindType("UnityEngine.Splines.SplineExtrude");

            _splinePackageAvailable = _splineContainerType != null && _splineType != null && _bezierKnotType != null;
            return _splinePackageAvailable;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static object CreateFloat3(float x, float y, float z)
        {
            if (_float3Type != null)
                return Activator.CreateInstance(_float3Type, x, y, z);
            return new Vector3(x, y, z);
        }

        private static Vector3 ParseVec3(string s)
        {
            return TypeParser.ParseVector3(s);
        }

        private static object CreateBezierKnot(Vector3 position, Vector3? tangentIn = null, Vector3? tangentOut = null)
        {
            var pos = CreateFloat3(position.x, position.y, position.z);
            var tIn = tangentIn.HasValue
                ? CreateFloat3(tangentIn.Value.x, tangentIn.Value.y, tangentIn.Value.z)
                : CreateFloat3(0, 0, 0);
            var tOut = tangentOut.HasValue
                ? CreateFloat3(tangentOut.Value.x, tangentOut.Value.y, tangentOut.Value.z)
                : CreateFloat3(0, 0, 0);

            // BezierKnot(float3 position, float3 tangentIn, float3 tangentOut, quaternion rotation)
            var identityQuat = _quaternionMathType != null
                ? _quaternionMathType.GetField("identity", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                : null;

            if (identityQuat != null)
                return Activator.CreateInstance(_bezierKnotType, pos, tIn, tOut, identityQuat);
            else
                return Activator.CreateInstance(_bezierKnotType, pos, tIn, tOut);
        }

        private static object CreateSpline(Dictionary<string, object> p)
        {
            if (!EnsureSplinePackage())
                throw new InvalidOperationException("Unity Splines package (com.unity.splines) is not installed. Please install it via Package Manager.");

            string name = GetStringParam(p, "name", "Spline");
            var knotStrings = GetStringListParam(p, "knots");
            bool closed = GetBoolParam(p, "closed");
            string tangentMode = GetStringParam(p, "tangent_mode", "AutoSmooth");

            if (knotStrings == null || knotStrings.Length == 0)
                throw new ArgumentException("knots array is required and must contain at least one position");

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "MCP: Create Spline");

            var container = go.AddComponent(_splineContainerType);

            // Get the Spline from the container
            var splineProp = _splineContainerType.GetProperty("Spline");
            var spline = splineProp?.GetValue(container);

            if (spline == null)
            {
                // Try Splines list
                var splinesProp = _splineContainerType.GetProperty("Splines");
                if (splinesProp != null)
                {
                    var splines = splinesProp.GetValue(container);
                    var countProp = splines.GetType().GetProperty("Count");
                    if (countProp != null && (int)countProp.GetValue(splines) > 0)
                    {
                        var indexer = splines.GetType().GetProperty("Item");
                        spline = indexer?.GetValue(splines, new object[] { 0 });
                    }
                }
            }

            if (spline == null)
                throw new InvalidOperationException("Failed to get Spline from SplineContainer");

            // Add knots
            var addMethod = _splineType.GetMethod("Add", new[] { _bezierKnotType });
            foreach (var knotStr in knotStrings)
            {
                var pos = ParseVec3(knotStr);
                var knot = CreateBezierKnot(pos);
                addMethod?.Invoke(spline, new[] { knot });
            }

            // Set closed
            var closedProp = _splineType.GetProperty("Closed");
            closedProp?.SetValue(spline, closed);

            // Set tangent mode if applicable
            if (tangentMode.ToLower() != "autosmooth")
            {
                var setTangentMode = FindSetTangentModeMethod();
                if (setTangentMode != null)
                {
                    var tangentModeType = FindType("UnityEngine.Splines.TangentMode");
                    if (tangentModeType != null)
                    {
                        object mode;
                        switch (tangentMode.ToLower())
                        {
                            case "linear": mode = Enum.Parse(tangentModeType, "Linear"); break;
                            case "bezier": mode = Enum.Parse(tangentModeType, "Bezier"); break;
                            default: mode = Enum.Parse(tangentModeType, "AutoSmooth"); break;
                        }

                        var countProp = _splineType.GetProperty("Count");
                        int count = countProp != null ? (int)countProp.GetValue(spline) : 0;
                        for (int i = 0; i < count; i++)
                        {
                            try { setTangentMode.Invoke(null, new object[] { spline, i, mode }); }
                            catch { /* Mode setting is optional */ }
                        }
                    }
                }
            }

            EditorUtility.SetDirty(go);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", name },
                { "path", GetGameObjectPath(go) },
                { "knotCount", knotStrings.Length },
                { "closed", closed },
                { "tangentMode", tangentMode }
            };
        }

        private static MethodInfo FindSetTangentModeMethod()
        {
            var utilType = FindType("UnityEngine.Splines.SplineUtility");
            if (utilType == null) return null;
            return utilType.GetMethod("SetTangentMode", BindingFlags.Public | BindingFlags.Static);
        }

        private static object AddSplineKnot(Dictionary<string, object> p)
        {
            if (!EnsureSplinePackage())
                throw new InvalidOperationException("Unity Splines package (com.unity.splines) is not installed.");

            string target = GetStringParam(p, "target");
            string posStr = GetStringParam(p, "position");
            int index = GetIntParam(p, "index", -1);
            string tangentInStr = GetStringParam(p, "tangent_in");
            string tangentOutStr = GetStringParam(p, "tangent_out");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(posStr))
                throw new ArgumentException("position is required");

            var go = FindGameObject(target);
            var container = go.GetComponent(_splineContainerType);
            if (container == null)
                throw new ArgumentException($"No SplineContainer found on: {target}");

            RecordUndo((UnityEngine.Object)container, "Add Spline Knot");

            var spline = GetSplineFromContainer(container);
            var pos = ParseVec3(posStr);
            Vector3? tIn = !string.IsNullOrEmpty(tangentInStr) ? ParseVec3(tangentInStr) : (Vector3?)null;
            Vector3? tOut = !string.IsNullOrEmpty(tangentOutStr) ? ParseVec3(tangentOutStr) : (Vector3?)null;

            var knot = CreateBezierKnot(pos, tIn, tOut);

            var countProp = _splineType.GetProperty("Count");
            int currentCount = countProp != null ? (int)countProp.GetValue(spline) : 0;

            if (index >= 0 && index < currentCount)
            {
                var insertMethod = _splineType.GetMethod("Insert", new[] { typeof(int), _bezierKnotType });
                insertMethod?.Invoke(spline, new object[] { index, knot });
            }
            else
            {
                var addMethod = _splineType.GetMethod("Add", new[] { _bezierKnotType });
                addMethod?.Invoke(spline, new[] { knot });
                index = currentCount;
            }

            EditorUtility.SetDirty(go);

            int newCount = countProp != null ? (int)countProp.GetValue(spline) : 0;

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "knotIndex", index },
                { "position", posStr },
                { "totalKnots", newCount }
            };
        }

        private static object SetSplineKnot(Dictionary<string, object> p)
        {
            if (!EnsureSplinePackage())
                throw new InvalidOperationException("Unity Splines package (com.unity.splines) is not installed.");

            string target = GetStringParam(p, "target");
            int knotIndex = GetIntParam(p, "knot_index", -1);
            string posStr = GetStringParam(p, "position");
            string tangentInStr = GetStringParam(p, "tangent_in");
            string tangentOutStr = GetStringParam(p, "tangent_out");
            string rotStr = GetStringParam(p, "rotation");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");
            if (knotIndex < 0)
                throw new ArgumentException("knot_index is required and must be >= 0");

            var go = FindGameObject(target);
            var container = go.GetComponent(_splineContainerType);
            if (container == null)
                throw new ArgumentException($"No SplineContainer found on: {target}");

            RecordUndo((UnityEngine.Object)container, "Set Spline Knot");

            var spline = GetSplineFromContainer(container);

            var countProp = _splineType.GetProperty("Count");
            int count = countProp != null ? (int)countProp.GetValue(spline) : 0;
            if (knotIndex >= count)
                throw new ArgumentException($"knot_index {knotIndex} out of range (spline has {count} knots)");

            // Get current knot via indexer
            var indexer = _splineType.GetProperty("Item");
            var currentKnot = indexer?.GetValue(spline, new object[] { knotIndex });
            if (currentKnot == null)
                throw new InvalidOperationException("Failed to get knot at index");

            // Read current values
            var posField = _bezierKnotType.GetField("Position");
            var tInField = _bezierKnotType.GetField("TangentIn");
            var tOutField = _bezierKnotType.GetField("TangentOut");
            var rotField = _bezierKnotType.GetField("Rotation");

            // Update position
            if (!string.IsNullOrEmpty(posStr))
            {
                var v = ParseVec3(posStr);
                posField?.SetValue(currentKnot, CreateFloat3(v.x, v.y, v.z));
            }

            if (!string.IsNullOrEmpty(tangentInStr))
            {
                var v = ParseVec3(tangentInStr);
                tInField?.SetValue(currentKnot, CreateFloat3(v.x, v.y, v.z));
            }

            if (!string.IsNullOrEmpty(tangentOutStr))
            {
                var v = ParseVec3(tangentOutStr);
                tOutField?.SetValue(currentKnot, CreateFloat3(v.x, v.y, v.z));
            }

            if (!string.IsNullOrEmpty(rotStr) && _quaternionMathType != null)
            {
                // Parse as x,y,z,w quaternion
                var parts = rotStr.Split(',');
                if (parts.Length == 4)
                {
                    float qx = float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    float qy = float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    float qz = float.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    float qw = float.Parse(parts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);

                    // Create Unity.Mathematics.quaternion from float4 components
                    var float4Type = FindType("Unity.Mathematics.float4");
                    if (float4Type != null)
                    {
                        var f4 = Activator.CreateInstance(float4Type, qx, qy, qz, qw);
                        var mathQuat = Activator.CreateInstance(_quaternionMathType, f4);
                        rotField?.SetValue(currentKnot, mathQuat);
                    }
                }
            }

            // Set the knot back
            indexer?.SetValue(spline, currentKnot, new object[] { knotIndex });

            EditorUtility.SetDirty(go);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "knotIndex", knotIndex },
                { "message", $"Updated knot {knotIndex} on {go.name}" }
            };
        }

        private static object GetSplineInfo(Dictionary<string, object> p)
        {
            if (!EnsureSplinePackage())
                throw new InvalidOperationException("Unity Splines package (com.unity.splines) is not installed.");

            string target = GetStringParam(p, "target");
            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");

            var go = FindGameObject(target);
            var container = go.GetComponent(_splineContainerType);
            if (container == null)
                throw new ArgumentException($"No SplineContainer found on: {target}");

            var spline = GetSplineFromContainer(container);

            var countProp = _splineType.GetProperty("Count");
            int count = countProp != null ? (int)countProp.GetValue(spline) : 0;

            var closedProp = _splineType.GetProperty("Closed");
            bool closed = closedProp != null && (bool)closedProp.GetValue(spline);

            // Get length via SplineUtility if available
            float length = 0f;
            var getLengthMethod = _splineType.GetMethod("GetLength");
            if (getLengthMethod != null)
            {
                var lengthVal = getLengthMethod.Invoke(spline, null);
                if (lengthVal is float fl) length = fl;
            }

            var knots = new List<object>();
            var indexer = _splineType.GetProperty("Item");
            var posField = _bezierKnotType.GetField("Position");
            var tInField = _bezierKnotType.GetField("TangentIn");
            var tOutField = _bezierKnotType.GetField("TangentOut");

            for (int i = 0; i < count; i++)
            {
                var knot = indexer?.GetValue(spline, new object[] { i });
                if (knot == null) continue;

                var knotInfo = new Dictionary<string, object> { { "index", i } };

                var pos = posField?.GetValue(knot);
                if (pos != null)
                {
                    var xField = pos.GetType().GetField("x");
                    var yField = pos.GetType().GetField("y");
                    var zField = pos.GetType().GetField("z");
                    if (xField != null)
                        knotInfo["position"] = $"{xField.GetValue(pos)},{yField.GetValue(pos)},{zField.GetValue(pos)}";
                }

                var tIn = tInField?.GetValue(knot);
                if (tIn != null)
                {
                    var xField = tIn.GetType().GetField("x");
                    var yField = tIn.GetType().GetField("y");
                    var zField = tIn.GetType().GetField("z");
                    if (xField != null)
                        knotInfo["tangentIn"] = $"{xField.GetValue(tIn)},{yField.GetValue(tIn)},{zField.GetValue(tIn)}";
                }

                var tOut = tOutField?.GetValue(knot);
                if (tOut != null)
                {
                    var xField = tOut.GetType().GetField("x");
                    var yField = tOut.GetType().GetField("y");
                    var zField = tOut.GetType().GetField("z");
                    if (xField != null)
                        knotInfo["tangentOut"] = $"{xField.GetValue(tOut)},{yField.GetValue(tOut)},{zField.GetValue(tOut)}";
                }

                knots.Add(knotInfo);
            }

            return new Dictionary<string, object>
            {
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "knotCount", count },
                { "closed", closed },
                { "length", length },
                { "knots", knots }
            };
        }

        private static object ExtrudeSplineMesh(Dictionary<string, object> p)
        {
            if (!EnsureSplinePackage())
                throw new InvalidOperationException("Unity Splines package (com.unity.splines) is not installed.");

            string target = GetStringParam(p, "target");
            string shape = GetStringParam(p, "shape", "flat");
            float width = GetFloatParam(p, "width", 1f);
            int segments = GetIntParam(p, "segments", 20);
            string materialPath = GetStringParam(p, "material_path");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target is required");

            var go = FindGameObject(target);
            var container = go.GetComponent(_splineContainerType);
            if (container == null)
                throw new ArgumentException($"No SplineContainer found on: {target}");

            RecordUndo(go, "Extrude Spline Mesh");

            bool usedSplineExtrude = false;

            // Try to use SplineExtrude component if available
            if (_splineExtrudeType != null)
            {
                try
                {
                    var extrude = go.GetComponent(_splineExtrudeType);
                    if (extrude == null)
                        extrude = Undo.AddComponent(go, _splineExtrudeType);

                    // Set segments
                    var segmentsProp = _splineExtrudeType.GetProperty("Segments")
                        ?? _splineExtrudeType.GetProperty("SegmentCount");
                    segmentsProp?.SetValue(extrude, segments);

                    // Set radius/width
                    var radiusProp = _splineExtrudeType.GetProperty("Radius");
                    radiusProp?.SetValue(extrude, width);

                    // Rebuild
                    var rebuildMethod = _splineExtrudeType.GetMethod("Rebuild");
                    rebuildMethod?.Invoke(extrude, null);

                    usedSplineExtrude = true;
                }
                catch
                {
                    usedSplineExtrude = false;
                }
            }

            // Fallback: generate mesh manually
            if (!usedSplineExtrude)
            {
                var spline = GetSplineFromContainer(container);
                var mesh = GenerateMeshAlongSpline(spline, shape, width, segments);

                var mf = go.GetComponent<MeshFilter>();
                if (mf == null) mf = Undo.AddComponent<MeshFilter>(go);
                mf.sharedMesh = mesh;

                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) mr = Undo.AddComponent<MeshRenderer>(go);
            }

            // Apply material
            if (!string.IsNullOrEmpty(materialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat != null)
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = mat;
                }
            }

            EditorUtility.SetDirty(go);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "shape", shape },
                { "width", width },
                { "segments", segments },
                { "usedSplineExtrude", usedSplineExtrude },
                { "message", $"Mesh extruded along spline on {go.name}" }
            };
        }

        private static object GetSplineFromContainer(object container)
        {
            var splineProp = _splineContainerType.GetProperty("Spline");
            var spline = splineProp?.GetValue(container);
            if (spline != null) return spline;

            var splinesProp = _splineContainerType.GetProperty("Splines");
            if (splinesProp != null)
            {
                var splines = splinesProp.GetValue(container);
                var countProp = splines.GetType().GetProperty("Count");
                if (countProp != null && (int)countProp.GetValue(splines) > 0)
                {
                    var indexer = splines.GetType().GetProperty("Item");
                    return indexer?.GetValue(splines, new object[] { 0 });
                }
            }

            throw new InvalidOperationException("Failed to get Spline from SplineContainer");
        }

        private static Mesh GenerateMeshAlongSpline(object spline, string shape, float width, int segments)
        {
            var mesh = new Mesh();
            mesh.name = "SplineExtrudedMesh";

            var countProp = _splineType.GetProperty("Count");
            int knotCount = countProp != null ? (int)countProp.GetValue(spline) : 0;
            if (knotCount < 2)
                throw new ArgumentException("Spline must have at least 2 knots to extrude a mesh");

            // Sample positions along the spline using knot positions
            var indexer = _splineType.GetProperty("Item");
            var posField = _bezierKnotType.GetField("Position");

            var positions = new List<Vector3>();
            for (int i = 0; i < knotCount; i++)
            {
                var knot = indexer?.GetValue(spline, new object[] { i });
                var pos = posField?.GetValue(knot);
                if (pos != null)
                {
                    var xf = pos.GetType().GetField("x");
                    var yf = pos.GetType().GetField("y");
                    var zf = pos.GetType().GetField("z");
                    float x = Convert.ToSingle(xf.GetValue(pos));
                    float y = Convert.ToSingle(yf.GetValue(pos));
                    float z = Convert.ToSingle(zf.GetValue(pos));
                    positions.Add(new Vector3(x, y, z));
                }
            }

            // Interpolate to get enough segments
            var sampledPoints = new List<Vector3>();
            for (int i = 0; i < positions.Count - 1; i++)
            {
                int segPerSpan = Mathf.Max(1, segments / (positions.Count - 1));
                for (int j = 0; j < segPerSpan; j++)
                {
                    float t = (float)j / segPerSpan;
                    sampledPoints.Add(Vector3.Lerp(positions[i], positions[i + 1], t));
                }
            }
            sampledPoints.Add(positions[positions.Count - 1]);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            int crossSectionVerts;

            switch (shape.ToLower())
            {
                case "tube":
                    crossSectionVerts = 8;
                    break;
                case "rail":
                    crossSectionVerts = 4;
                    break;
                default: // flat
                    crossSectionVerts = 2;
                    break;
            }

            float halfWidth = width * 0.5f;

            for (int i = 0; i < sampledPoints.Count; i++)
            {
                Vector3 point = sampledPoints[i];
                Vector3 forward = Vector3.forward;
                if (i < sampledPoints.Count - 1)
                    forward = (sampledPoints[i + 1] - point).normalized;
                else if (i > 0)
                    forward = (point - sampledPoints[i - 1]).normalized;

                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.Cross(Vector3.right, forward).normalized;
                Vector3 up = Vector3.Cross(forward, right).normalized;

                float vCoord = (float)i / (sampledPoints.Count - 1);

                if (shape.ToLower() == "tube")
                {
                    for (int j = 0; j < crossSectionVerts; j++)
                    {
                        float angle = (float)j / crossSectionVerts * Mathf.PI * 2f;
                        Vector3 offset = right * Mathf.Cos(angle) * halfWidth + up * Mathf.Sin(angle) * halfWidth;
                        vertices.Add(point + offset);
                        uvs.Add(new Vector2((float)j / crossSectionVerts, vCoord));
                    }
                }
                else if (shape.ToLower() == "rail")
                {
                    vertices.Add(point - right * halfWidth);
                    vertices.Add(point - right * halfWidth + up * width);
                    vertices.Add(point + right * halfWidth + up * width);
                    vertices.Add(point + right * halfWidth);
                    uvs.Add(new Vector2(0, vCoord));
                    uvs.Add(new Vector2(0.33f, vCoord));
                    uvs.Add(new Vector2(0.66f, vCoord));
                    uvs.Add(new Vector2(1, vCoord));
                }
                else // flat
                {
                    vertices.Add(point - right * halfWidth);
                    vertices.Add(point + right * halfWidth);
                    uvs.Add(new Vector2(0, vCoord));
                    uvs.Add(new Vector2(1, vCoord));
                }
            }

            // Build triangles
            for (int i = 0; i < sampledPoints.Count - 1; i++)
            {
                for (int j = 0; j < crossSectionVerts; j++)
                {
                    int current = i * crossSectionVerts + j;
                    int next = i * crossSectionVerts + (j + 1) % crossSectionVerts;
                    int currentNext = (i + 1) * crossSectionVerts + j;
                    int nextNext = (i + 1) * crossSectionVerts + (j + 1) % crossSectionVerts;

                    // For flat shape, only connect left-right, not wrap around
                    if (shape.ToLower() == "flat" && j >= crossSectionVerts - 1) continue;
                    if (shape.ToLower() == "rail" && j >= crossSectionVerts - 1) continue;

                    triangles.Add(current);
                    triangles.Add(currentNext);
                    triangles.Add(next);

                    triangles.Add(next);
                    triangles.Add(currentNext);
                    triangles.Add(nextNext);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
