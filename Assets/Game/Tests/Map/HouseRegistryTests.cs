using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Map
{
    public class HouseRegistryTests
    {
        [Test]
        public void Build_AssignsDropoffNodeFromNearestRoad()
        {
            var skeleton = ScriptableObject.CreateInstance<MapSkeleton>();
            skeleton.roadNodePositions = new[]
            {
                new Vector3(-5f, 0f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, 0f)
            };
            skeleton.houseLots = new[]
            {
                new HouseLot
                {
                    origin = new Vector2Int(-3, 1),
                    footprint = MapGridSpec.HouseFootprintVertical
                }
            };

            var graph = RoadGraph.BuildFromPolylines(new[] { skeleton.roadNodePositions });
            var houses = HouseRegistry.Build(skeleton, graph);

            Assert.AreEqual(1, houses.Count);
            var house = houses.GetByIndex(0);
            Assert.AreEqual(new Vector2Int(-3, 1), house.Origin);
            Assert.AreEqual(new Vector3(-3f, 0f, 1.5f), house.Center);
            Assert.AreEqual(MapGridSpec.HouseFootprintVertical, house.Footprint);
            Assert.AreEqual(1, house.DropoffNodeId);
        }

        [Test]
        public void DemandSpawner_UsesHouseDropoffNode()
        {
            var skeleton = ScriptableObject.CreateInstance<MapSkeleton>();
            skeleton.roadNodePositions = new[]
            {
                Vector3.zero,
                Vector3.right * 5f
            };
            skeleton.houseLots = new[]
            {
                new HouseLot
                {
                    origin = new Vector2Int(2, 1),
                    footprint = MapGridSpec.HouseFootprintVertical
                }
            };

            var graph = RoadGraph.BuildFromPolylines(new[] { skeleton.roadNodePositions });
            var houses = HouseRegistry.Build(skeleton, graph);

            var waveDef = ScriptableObject.CreateInstance<DemandWaveDef>();
            waveDef.waves.Add(new DemandWaveEntry
            {
                daySecond = 1f,
                module = ServiceModule.Food,
                sizeBand = JobSizeBand.Small,
                count = 1
            });

            var moduleDef = ScriptableObject.CreateInstance<ServiceModuleDef>();
            moduleDef.module = ServiceModule.Food;
            moduleDef.basePatienceSeconds = 60f;
            moduleDef.graceSeconds = 10f;

            var moduleDefs = new Dictionary<ServiceModule, ServiceModuleDef>
            {
                { ServiceModule.Food, moduleDef }
            };

            var spawner = new DemandSpawner(waveDef, 100f, moduleDefs, graph, houses);
            spawner.Tick(0.02f);

            Assert.AreEqual(1, spawner.Orders.Count);
            Assert.AreEqual(0, spawner.Orders[0].DestinationHouseId);
            Assert.AreEqual(houses.GetByIndex(0).DropoffNodeId, spawner.Orders[0].DropoffNode);
            Assert.AreEqual(-1, spawner.Orders[0].PickupNode);
        }
    }
}
