using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Demand;
using TrafficSim.Fleet;
using TrafficSim.Hubs;
using TrafficSim.Map;
using UnityEngine;
using ZLinq;

namespace TrafficSim.Core.Linq
{
    /// <summary>
    /// Domain-specific ZLinq queries. Prefer these over ad-hoc LINQ chains in sim hot paths.
    /// Plain for-loops on List&lt;T&gt; remain valid when no filtering/allocation benefit exists.
    /// </summary>
    public static class SimLinq
    {
        public static int FindNearestNodeIndex(RoadGraph graph, Vector3 worldPosition) =>
            ValueEnumerable.Range(0, graph.NodeCount)
                .MinBy(i => Vector3.SqrMagnitude(graph.GetNodePosition(i) - worldPosition));

        public static void ForEachVehicle(IEnumerable<VehicleInstance> vehicles, Action<VehicleInstance> action)
        {
            foreach (var vehicle in vehicles.AsValueEnumerable())
                action(vehicle);
        }

        public static void ForEachHub(IEnumerable<HubInstance> hubs, Action<HubInstance> action)
        {
            foreach (var hub in hubs.AsValueEnumerable())
                action(hub);
        }

        public static int SumHubCapacity(IEnumerable<HubInstance> hubs) =>
            hubs.AsValueEnumerable().Sum(h => h.Capacity);

        public static void CollectPendingOrders(IList<OrderInstance> destination, IReadOnlyList<OrderInstance> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var order = source[i];
                if (order.State == OrderState.Pending)
                    destination.Add(order);
            }
        }

        public static void TickPatienceForOrders(IReadOnlyList<OrderInstance> orders, float deltaTime)
        {
            for (var i = 0; i < orders.Count; i++)
                orders[i].TickPatience(deltaTime);
        }

        public static bool TryFindNearestEligibleVehicle(
            IEnumerable<VehicleInstance> vehicles,
            RoadGraph graph,
            OrderInstance order,
            Func<VehicleInstance, OrderInstance, IReadOnlyList<int>> buildPath,
            out VehicleInstance nearest)
        {
            nearest = null;
            var bestDistance = float.MaxValue;

            foreach (var vehicle in vehicles.AsValueEnumerable())
            {
                if (vehicle.Module != order.Module || !vehicle.IsDispatchEligible)
                    continue;

                if (!vehicle.Def.CanServe(order.Module, order.SizeBand))
                    continue;

                var path = buildPath(vehicle, order);
                if (path == null || path.Count == 0)
                    continue;

                var distance = graph.EstimatePathDistance(path);
                if (distance > vehicle.Def.maxRange)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = vehicle;
                }
            }

            return nearest != null;
        }
    }
}
