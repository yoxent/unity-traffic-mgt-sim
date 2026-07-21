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

        public float GetEdgeDistance(int fromNodeId, int toNodeId)
        {
            foreach (var (neighborId, distance) in _adjacency[fromNodeId])
            {
                if (neighborId == toNodeId)
                    return distance;
            }

            throw new ArgumentException($"No edge from node {fromNodeId} to node {toNodeId}.");
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
