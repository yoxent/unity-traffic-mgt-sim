using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Demand;
using TrafficSim.Events;
using TrafficSim.Fleet;
using TrafficSim.Map;

namespace TrafficSim.Dispatch
{
    public sealed class DispatchService
    {
        readonly FleetManager _fleet;
        readonly RoadGraph _graph;
        readonly IList<OrderInstance> _orders;
        readonly OrderEventChannel _orderAssignedChannel;
        readonly Dictionary<int, ActiveRoute> _activeRoutes = new();

        struct ActiveRoute
        {
            public VehicleInstance Vehicle;
            public OrderInstance Order;
            public VehiclePathAgent Agent;
        }

        public DispatchService(
            FleetManager fleet,
            RoadGraph graph,
            IList<OrderInstance> orders,
            OrderEventChannel orderAssignedChannel = null)
        {
            _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _orderAssignedChannel = orderAssignedChannel;
        }

        public void Tick(float deltaTime)
        {
            TryAssignPendingOrders();
            TickActiveRoutes(deltaTime);
        }

        void TryAssignPendingOrders()
        {
            for (var i = 0; i < _orders.Count; i++)
            {
                var order = _orders[i];
                if (order.State != OrderState.Pending)
                    continue;

                if (!TryFindNearestEligibleVehicle(order, out var vehicle))
                    continue;

                if (!TryBuildJobPath(_graph, vehicle.CurrentNodeId, order.PickupNode, order.DropoffNode, out var path))
                    continue;

                if (!vehicle.TryAssignOrder(order))
                    continue;

                order.MarkAssigned();
                StartRoute(vehicle, order, path);
                _orderAssignedChannel?.Raise(new OrderEventPayload(order.Id, order.Module));
            }
        }

        void TickActiveRoutes(float deltaTime)
        {
            foreach (var pair in _activeRoutes)
                pair.Value.Agent.Tick(deltaTime);
        }

        void StartRoute(VehicleInstance vehicle, OrderInstance order, IReadOnlyList<int> path)
        {
            var agent = vehicle.PathAgent;
            agent.SetPath(path, _graph);

            _activeRoutes[vehicle.Id] = new ActiveRoute
            {
                Vehicle = vehicle,
                Order = order,
                Agent = agent
            };

            agent.Configure(_fleet.GetEffectiveSpeed(vehicle), () => CompleteRoute(vehicle.Id));
            vehicle.SetLocation(agent.Position, path[0]);
        }

        void CompleteRoute(int vehicleId)
        {
            if (!_activeRoutes.TryGetValue(vehicleId, out var route))
                return;

            _activeRoutes.Remove(vehicleId);

            var dropoffNode = route.Order.DropoffNode;
            route.Vehicle.SetLocation(_graph.GetNodePosition(dropoffNode), dropoffNode);
            route.Vehicle.CompleteJob();
            route.Order.MarkCompleted();
        }

        bool TryFindNearestEligibleVehicle(OrderInstance order, out VehicleInstance nearest)
        {
            nearest = null;
            var bestDistance = float.MaxValue;

            foreach (var vehicle in _fleet.GetAllVehicles())
            {
                if (vehicle.Module != order.Module || !vehicle.IsDispatchEligible)
                    continue;

                if (!vehicle.Def.CanServe(order.Module, order.SizeBand))
                    continue;

                if (!TryBuildJobPath(_graph, vehicle.CurrentNodeId, order.PickupNode, order.DropoffNode, out var path))
                    continue;

                var distance = _graph.EstimatePathDistance(path);
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

        static bool TryBuildJobPath(
            RoadGraph graph,
            int fromNodeId,
            int pickupNodeId,
            int dropoffNodeId,
            out List<int> path)
        {
            path = null;

            var toPickup = graph.FindPath(fromNodeId, pickupNodeId);
            if (toPickup.Count == 0)
                return false;

            var toDropoff = graph.FindPath(pickupNodeId, dropoffNodeId);
            if (toDropoff.Count == 0)
                return false;

            path = new List<int>(toPickup);
            for (var i = 1; i < toDropoff.Count; i++)
                path.Add(toDropoff[i]);

            return path.Count > 0;
        }
    }
}
