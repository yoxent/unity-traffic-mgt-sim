using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Dispatch;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Dispatch
{
    public class VehiclePathAgentTests
    {
        [Test]
        public void Tick_ReachesFinalNode_ReturnsTrueAndInvokesCallback()
        {
            var graph = RoadGraph.BuildLineGraph(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f)
            });

            var arrived = false;
            var agent = new VehiclePathAgent();
            agent.SetPath(new[] { 0, 1 }, graph);
            agent.Configure(10f, () => arrived = true);

            Assert.IsFalse(agent.Tick(0.5f));
            Assert.AreEqual(new Vector3(5f, 0f, 0f), agent.Position);

            Assert.IsTrue(agent.Tick(0.5f));
            Assert.AreEqual(new Vector3(10f, 0f, 0f), agent.Position);
            Assert.IsTrue(arrived);
        }

        [Test]
        public void Tick_MultiSegmentPath_AdvancesThroughNodes()
        {
            var graph = RoadGraph.BuildLineGraph(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(20f, 0f, 0f)
            });

            var agent = new VehiclePathAgent();
            agent.SetPath(new[] { 0, 1, 2 }, graph);
            agent.Configure(10f, null);

            Assert.IsFalse(agent.Tick(1f));
            Assert.AreEqual(new Vector3(10f, 0f, 0f), agent.Position);

            Assert.IsTrue(agent.Tick(1f));
            Assert.AreEqual(new Vector3(20f, 0f, 0f), agent.Position);
        }
    }
}
