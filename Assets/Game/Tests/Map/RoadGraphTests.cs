using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Data;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Map
{
    public class RoadGraphTests
    {
        [Test]
        public void FindPath_LineGraph_AtoC_ReturnsABC()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(20f, 0f, 0f)
            };

            var graph = RoadGraph.BuildLineGraph(positions);
            var path = graph.FindPath(0, 2);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, path);
        }

        [Test]
        public void Load_SkeletonWithSequentialNodes_BuildsLineGraph()
        {
            var skeleton = ScriptableObject.CreateInstance<MapSkeleton>();
            skeleton.roadNodePositions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(20f, 0f, 0f)
            };

            var graph = MapLoader.Load(skeleton).Graph;
            var path = graph.FindPath(0, 2);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, path);
        }

        [Test]
        public void BuildFromPolylines_BranchAtJunction_DoesNotDetourMainRoad()
        {
            var polylines = new IReadOnlyList<Vector3>[]
            {
                new[]
                {
                    new Vector3(-5f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(5f, 0f, 0f),
                    new Vector3(10f, 0f, 0f)
                },
                new[]
                {
                    new Vector3(5f, 0f, 0f),
                    new Vector3(5f, 0f, -3f)
                }
            };

            var graph = RoadGraph.BuildFromPolylines(polylines);
            var westNode = FindNodeAt(graph, new Vector3(-5f, 0f, 0f));
            var eastNode = FindNodeAt(graph, new Vector3(10f, 0f, 0f));
            var spurNode = FindNodeAt(graph, new Vector3(5f, 0f, -3f));

            var path = graph.FindPath(westNode, eastNode);
            CollectionAssert.AreEqual(new[] { westNode, FindNodeAt(graph, new Vector3(0f, 0f, 0f)), FindNodeAt(graph, new Vector3(5f, 0f, 0f)), eastNode }, path);
            Assert.IsTrue(graph.HasEdge(FindNodeAt(graph, new Vector3(5f, 0f, 0f)), spurNode));
        }

        [Test]
        public void Resolve_TutorialTJunctionAtFiveZero_UsesEastWestSouthTile()
        {
            var roads = new HashSet<Vector2Int>();
            MapGridSpec.CollectLineCells(new Vector3(-5f, 0f, 0f), new Vector3(10f, 0f, 0f), roads);
            MapGridSpec.CollectLineCells(new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, -3f), roads);

            var placement = RoadTileResolver.Resolve(new Vector2Int(5, 0), roads);

            Assert.AreEqual(RoadTileKind.TJunction, placement.Kind);
            Assert.AreEqual(0f, placement.YRotation);
        }

        static int FindNodeAt(RoadGraph graph, Vector3 position)
        {
            var cell = MapGridSpec.WorldToCell(position);
            for (var i = 0; i < graph.NodeCount; i++)
            {
                if (MapGridSpec.WorldToCell(graph.GetNodePosition(i)) == cell)
                    return i;
            }

            Assert.Fail($"Node not found at {position}");
            return -1;
        }
    }
}
