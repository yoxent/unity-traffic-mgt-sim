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

            var graph = MapLoader.Load(skeleton);
            var path = graph.FindPath(0, 2);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, path);
        }
    }
}
