using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Map
{
    public sealed class RoadGraph
    {
        readonly List<Vector3> _nodePositions = new();
        readonly List<List<(int neighborId, float distance)>> _adjacency = new();

        public int NodeCount => _nodePositions.Count;

        public Vector3 GetNodePosition(int nodeId) => _nodePositions[nodeId];

        public void ForEachUndirectedEdge(Action<int, int> action)
        {
            if (action == null)
                return;

            for (var from = 0; from < _adjacency.Count; from++)
            {
                var neighbors = _adjacency[from];
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var to = neighbors[i].neighborId;
                    if (from < to)
                        action(from, to);
                }
            }
        }

        public float GetEdgeDistance(int fromNodeId, int toNodeId)
        {
            foreach (var (neighborId, distance) in _adjacency[fromNodeId])
            {
                if (neighborId == toNodeId)
                    return distance;
            }

            throw new ArgumentException($"No edge from node {fromNodeId} to node {toNodeId}.");
        }

        public bool HasEdge(int fromNodeId, int toNodeId)
        {
            if (fromNodeId < 0 || fromNodeId >= NodeCount || toNodeId < 0 || toNodeId >= NodeCount)
                return false;

            foreach (var (neighborId, _) in _adjacency[fromNodeId])
            {
                if (neighborId == toNodeId)
                    return true;
            }

            return false;
        }

        public static RoadGraph BuildFromPolylines(IReadOnlyList<IReadOnlyList<Vector3>> polylines)
        {
            var graph = new RoadGraph();
            var nodeIndexByCell = new Dictionary<Vector2Int, int>();

            if (polylines == null || polylines.Count == 0)
                throw new ArgumentException("At least one polyline with nodes is required.", nameof(polylines));

            var hasNodes = false;
            for (var p = 0; p < polylines.Count; p++)
            {
                if (polylines[p] != null && polylines[p].Count > 0)
                {
                    hasNodes = true;
                    break;
                }
            }

            if (!hasNodes)
                throw new ArgumentException("At least one polyline with nodes is required.", nameof(polylines));

            for (var p = 0; p < polylines.Count; p++)
            {
                var polyline = polylines[p];
                if (polyline == null || polyline.Count == 0)
                    continue;

                var previous = GetOrAddNode(graph, nodeIndexByCell, polyline[0]);
                for (var i = 1; i < polyline.Count; i++)
                {
                    var current = GetOrAddNode(graph, nodeIndexByCell, polyline[i]);
                    if (previous != current && !graph.HasEdge(previous, current))
                    {
                        var distance = Vector3.Distance(
                            graph.GetNodePosition(previous),
                            graph.GetNodePosition(current));
                        graph.AddUndirectedEdge(previous, current, distance);
                    }

                    previous = current;
                }
            }

            return graph;
        }

        static int GetOrAddNode(RoadGraph graph, Dictionary<Vector2Int, int> nodeIndexByCell, Vector3 position)
        {
            var cell = MapGridSpec.WorldToCell(position);
            if (nodeIndexByCell.TryGetValue(cell, out var nodeId))
                return nodeId;

            nodeId = graph.NodeCount;
            graph.AddNode(MapGridSpec.CellCenter(cell));
            nodeIndexByCell[cell] = nodeId;
            return nodeId;
        }

        public static RoadGraph BuildLineGraph(IReadOnlyList<Vector3> positions)
        {
            if (positions == null || positions.Count == 0)
                throw new ArgumentException("At least one node position is required.", nameof(positions));

            var graph = new RoadGraph();

            for (var i = 0; i < positions.Count; i++)
                graph.AddNode(positions[i]);

            for (var i = 0; i < positions.Count - 1; i++)
            {
                var distance = Vector3.Distance(positions[i], positions[i + 1]);
                graph.AddUndirectedEdge(i, i + 1, distance);
            }

            return graph;
        }

        public void AddNode(Vector3 position)
        {
            _nodePositions.Add(position);
            _adjacency.Add(new List<(int neighborId, float distance)>());
        }

        public void AddUndirectedEdge(int fromNodeId, int toNodeId, float distance)
        {
            _adjacency[fromNodeId].Add((toNodeId, distance));
            _adjacency[toNodeId].Add((fromNodeId, distance));
        }

        public float EstimatePathDistance(IReadOnlyList<int> path)
        {
            if (path == null || path.Count < 2)
                return 0f;

            var total = 0f;
            for (var i = 0; i < path.Count - 1; i++)
                total += GetEdgeDistance(path[i], path[i + 1]);

            return total;
        }

        public float EstimatePathDistance(int fromNodeId, int toNodeId) =>
            EstimatePathDistance(FindPath(fromNodeId, toNodeId));

        public IReadOnlyList<int> FindPath(int fromNodeId, int toNodeId)
        {
            if (fromNodeId == toNodeId)
                return new[] { fromNodeId };

            if (fromNodeId < 0 || fromNodeId >= NodeCount || toNodeId < 0 || toNodeId >= NodeCount)
                return Array.Empty<int>();

            var visited = new bool[NodeCount];
            var previous = new int[NodeCount];
            for (var i = 0; i < previous.Length; i++)
                previous[i] = -1;

            var queue = new Queue<int>();
            queue.Enqueue(fromNodeId);
            visited[fromNodeId] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == toNodeId)
                    return ReconstructPath(previous, fromNodeId, toNodeId);

                foreach (var (neighborId, _) in _adjacency[current])
                {
                    if (visited[neighborId])
                        continue;

                    visited[neighborId] = true;
                    previous[neighborId] = current;
                    queue.Enqueue(neighborId);
                }
            }

            return Array.Empty<int>();
        }

        static IReadOnlyList<int> ReconstructPath(int[] previous, int fromNodeId, int toNodeId)
        {
            var path = new List<int>();
            for (var node = toNodeId; node != -1; node = previous[node])
                path.Add(node);

            path.Reverse();
            return path.Count > 0 && path[0] == fromNodeId ? path : Array.Empty<int>();
        }
    }
}
