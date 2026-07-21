using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Core.Contracts;
using TrafficSim.Core.Linq;
using TrafficSim.Demand;
using TrafficSim.Events;
using TrafficSim.Fleet;
using TrafficSim.Map;

namespace TrafficSim.Dispatch
{
    public sealed class DispatchService : IDispatchService
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

        public void Tick() => TryAssignPendingOrders();

        public void Tick(float deltaTime)
        {
            TryAssignPendingOrders();
            TickPathAgents(deltaTime);
        }

        public void TickPathAgents(float deltaTime) => TickActiveRoutes(deltaTime);

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
            var count = _activeRoutes.Count;
            if (count == 0)
                return;

            var agents = new VehiclePathAgent[count];
            var index = 0;
            foreach (var pair in _activeRoutes)
                agents[index++] = pair.Value.Agent;

            for (var i = 0; i < agents.Length; i++)
                agents[i].Tick(deltaTime);
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

        bool TryFindNearestEligibleVehicle(OrderInstance order, out VehicleInstance nearest) =>
            SimLinq.TryFindNearestEligibleVehicle(
                _fleet.GetAllVehicles(),
                _graph,
                order,
                BuildPathForDispatch,
                out nearest);

        IReadOnlyList<int> BuildPathForDispatch(VehicleInstance vehicle, OrderInstance order) =>
            TryBuildJobPath(_graph, vehicle.CurrentNodeId, order.PickupNode, order.DropoffNode, out var path)
                ? path
                : null;

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
