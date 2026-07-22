using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Demand
{
    public class DemandSpawnerTests
    {
        const float DayLengthSeconds = 300f;

        static (DemandWaveDef waveDef, ServiceModuleDef moduleDef, RoadGraph graph) CreateScenario()
        {
            var waveDef = ScriptableObject.CreateInstance<DemandWaveDef>();
            waveDef.waves.Add(new DemandWaveEntry
            {
                daySecond = 30f,
                module = ServiceModule.Food,
                sizeBand = JobSizeBand.Small,
                count = 1
            });

            var moduleDef = ScriptableObject.CreateInstance<ServiceModuleDef>();
            moduleDef.module = ServiceModule.Food;
            moduleDef.basePatienceSeconds = 120f;
            moduleDef.graceSeconds = 30f;

            var graph = RoadGraph.BuildLineGraph(new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.right * 2f
            });

            return (waveDef, moduleDef, graph);
        }

        static DemandSpawner CreateSpawner(
            DemandWaveDef waveDef,
            ServiceModuleDef moduleDef,
            RoadGraph graph,
            HouseRegistry houses = null)
        {
            var moduleDefs = new Dictionary<ServiceModule, ServiceModuleDef>
            {
                { ServiceModule.Food, moduleDef }
            };

            return new DemandSpawner(
                waveDef,
                DayLengthSeconds,
                moduleDefs,
                graph,
                houses ?? new HouseRegistry(System.Array.Empty<HouseInstance>()));
        }

        [Test]
        public void Tick_PastWaveThreshold_SpawnsOneOrder()
        {
            var (waveDef, moduleDef, graph) = CreateScenario();
            var spawner = CreateSpawner(waveDef, moduleDef, graph);

            spawner.Tick(0.05f);
            Assert.AreEqual(0, spawner.Orders.Count);

            spawner.Tick(0.15f);
            Assert.AreEqual(1, spawner.Orders.Count);
            Assert.AreEqual(ServiceModule.Food, spawner.Orders[0].Module);
            Assert.AreEqual(JobSizeBand.Small, spawner.Orders[0].SizeBand);
            Assert.AreEqual(OrderState.Pending, spawner.Orders[0].State);
        }

        [Test]
        public void GetUpcomingCheckpoints_ReturnsNextThreeBeforeSpawn()
        {
            var (waveDef, moduleDef, graph) = CreateScenario();
            waveDef.waves.Add(new DemandWaveEntry
            {
                daySecond = 90f,
                module = ServiceModule.Food,
                sizeBand = JobSizeBand.OnePassenger,
                count = 2
            });
            waveDef.waves.Add(new DemandWaveEntry
            {
                daySecond = 150f,
                module = ServiceModule.Car,
                sizeBand = JobSizeBand.OneToFourPassengers,
                count = 1
            });
            waveDef.waves.Add(new DemandWaveEntry
            {
                daySecond = 210f,
                module = ServiceModule.Food,
                sizeBand = JobSizeBand.Small,
                count = 3
            });

            var spawner = CreateSpawner(waveDef, moduleDef, graph);
            spawner.Tick(0.05f);

            var checkpoints = spawner.GetUpcomingCheckpoints();
            Assert.AreEqual(3, checkpoints.Count);
            Assert.AreEqual(0.1f, checkpoints[0].DayFraction, 0.001f);
            Assert.AreEqual(ServiceModule.Food, checkpoints[0].Module);
            Assert.AreEqual(1, checkpoints[0].Count);
        }

        [Test]
        public void OrderInstance_TickPatience_ExpiresAfterGraceAndPatienceElapse()
        {
            var order = new OrderInstance(
                1,
                ServiceModule.Food,
                JobSizeBand.Small,
                0,
                1,
                patienceTotal: 10f,
                graceTotal: 5f);

            Assert.AreEqual(1f, order.RemainingFraction, 0.001f);

            order.TickPatience(5f);
            Assert.AreEqual(1f, order.RemainingFraction, 0.001f);
            Assert.AreEqual(OrderState.Pending, order.State);

            order.TickPatience(10f);
            Assert.AreEqual(0f, order.RemainingFraction, 0.001f);
            Assert.AreEqual(OrderState.Expired, order.State);
        }
    }
}
