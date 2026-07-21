using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Dispatch;
using TrafficSim.Events;
using TrafficSim.Fleet;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Dispatch
{
    public class DispatchServiceTests
    {
        static VehicleDef CreateCarMotorbikeDef(float maxRange = 100f)
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            def.type = VehicleType.Motorbike;
            def.maxRange = maxRange;
            def.cooldownSeconds = 0f;
            def.allowedModules = new[] { ServiceModule.Car };
            def.allowedSizeBands = new[] { JobSizeBand.OnePassenger };
            return def;
        }

        static (FleetManager fleet, VehicleInstance vehicle, RoadGraph graph, List<OrderInstance> orders) CreateScenario()
        {
            var graph = RoadGraph.BuildLineGraph(new[]
            {
                Vector3.zero,
                Vector3.right * 10f,
                Vector3.right * 20f
            });

            var state = new RunState { Money = 1000f };
            var fleet = new FleetManager(state);
            var def = CreateCarMotorbikeDef();
            Assert.IsTrue(fleet.BuyVehicle(ServiceModule.Car, def));

            var vehicle = fleet.GetVehicles(ServiceModule.Car, VehicleType.Motorbike)[0];
            vehicle.SetLocation(graph.GetNodePosition(0), 0);

            var orders = new List<OrderInstance>
            {
                new(
                    1,
                    ServiceModule.Car,
                    JobSizeBand.OnePassenger,
                    pickupNode: 0,
                    dropoffNode: 2,
                    patienceTotal: 120f,
                    graceTotal: 30f)
            };

            return (fleet, vehicle, graph, orders);
        }

        [Test]
        public void Tick_OneIdleMotorbikeAndOnePassengerCarOrder_AssignsOrder()
        {
            var (fleet, vehicle, graph, orders) = CreateScenario();
            var order = orders[0];
            var dispatch = new DispatchService(fleet, graph, orders);

            Assert.AreEqual(OrderState.Pending, order.State);
            Assert.IsTrue(vehicle.IsDispatchEligible);
            Assert.IsNull(vehicle.CurrentOrderId);

            dispatch.Tick();

            Assert.AreEqual(OrderState.Assigned, order.State);
            Assert.AreEqual(order.Id, vehicle.CurrentOrderId);
            Assert.AreEqual(VehicleState.EnRoute, vehicle.State);
        }

        [Test]
        public void Tick_Assignment_RaisesOrderAssignedEvent()
        {
            var (fleet, _, graph, orders) = CreateScenario();
            var order = orders[0];
            var channel = ScriptableObject.CreateInstance<OrderEventChannel>();
            var raised = false;
            OrderEventPayload payload = default;
            channel.Register(p =>
            {
                payload = p;
                raised = true;
            });

            var dispatch = new DispatchService(fleet, graph, orders, channel);
            dispatch.Tick();

            Assert.IsTrue(raised);
            Assert.AreEqual(order.Id, payload.OrderId);
            Assert.AreEqual(ServiceModule.Car, payload.Module);
        }
    }
}
