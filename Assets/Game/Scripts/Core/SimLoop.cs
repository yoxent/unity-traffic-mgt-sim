using System;
using System.Collections.Generic;
using TrafficSim.Core.Linq;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Events;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Core
{
    public sealed class SimLoop
    {
        readonly SimSession _session;
        readonly MapSkeleton _mapSkeleton;
        readonly OrderEventChannel _orderCompletedChannel;
        readonly GameEventChannel _runFailedChannel;
        readonly HashSet<ServiceModule> _initializedModules = new();
        readonly HashSet<int> _processedCompletions = new();
        readonly HashSet<int> _processedExpirations = new();

        RunPhase _lastPhase = RunPhase.Playing;

        public SimLoop(
            SimSession session,
            MapSkeleton mapSkeleton,
            OrderEventChannel orderCompletedChannel,
            GameEventChannel runFailedChannel)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _mapSkeleton = mapSkeleton;
            _orderCompletedChannel = orderCompletedChannel;
            _runFailedChannel = runFailedChannel;
        }

        public void Tick(float deltaTime)
        {
            if (_session.State == null || _session.Clock == null)
                return;

            EnsureUnlockedModuleInfrastructure();
            MonitorRunFailure();

            if (_session.State.Phase != RunPhase.Playing)
                return;

            _session.Clock.Advance(deltaTime);

            // DayEnded may flip phase to EOD / Failed mid-tick.
            if (_session.State.Phase != RunPhase.Playing)
                return;

            _session.Demand.Tick(_session.Clock.DayFraction);
            RouteNewOrders();
            TickOrderPatience(deltaTime);
            TickFleetCooldowns(deltaTime);

            _session.Hubs.Tick(deltaTime);

            RefreshDispatchOrders();
            _session.Dispatch.Tick();
            _session.Overload.Tick();
            _session.Dispatch.TickPathAgents(deltaTime);

            ProcessOrderOutcomes();
        }

        public void EnsureUnlockedModuleInfrastructure()
        {
            // Snapshot — module init must not run against a live HashSet enumerator.
            var unlocked = new List<ServiceModule>(_session.State.UnlockedModules);
            for (var i = 0; i < unlocked.Count; i++)
                TryInitializeModule(unlocked[i]);
        }

        void TryInitializeModule(ServiceModule module)
        {
            if (_initializedModules.Contains(module))
                return;

            if (!_session.ModuleDefLookup.TryGetValue(module, out var moduleDef) ||
                !_session.HubDefLookup.TryGetValue(module, out var hubDef) ||
                !_session.VehicleDefLookup.TryGetValue(moduleDef.starterVehicleType, out var vehicleDef))
            {
                return;
            }

            var slotId = _initializedModules.Count;
            if (slotId > 0)
                _session.Hubs.UnlockSlot(slotId);

            if (!_session.Hubs.PlaceHub(hubDef, slotId))
                return;

            _session.ActiveHubDefs.Add(hubDef);

            for (var i = 0; i < moduleDef.starterVehicleCount; i++)
                _session.Fleet.BuyVehicle(module, vehicleDef);

            PositionFleetAtSlot(module, slotId);
            _initializedModules.Add(module);
        }

        void PositionFleetAtSlot(ServiceModule module, int slotId)
        {
            if (_mapSkeleton == null ||
                _mapSkeleton.hubSlotPositions == null ||
                slotId < 0 ||
                slotId >= _mapSkeleton.hubSlotPositions.Length)
            {
                return;
            }

            var nodeId = SimLinq.FindNearestNodeIndex(_session.Graph, _mapSkeleton.hubSlotPositions[slotId]);
            var position = _session.Graph.GetNodePosition(nodeId);

            SimLinq.ForEachVehicle(_session.Fleet.GetAllVehicles(), vehicle =>
            {
                if (vehicle.Module == module && vehicle.State == VehicleState.Idle)
                    vehicle.SetLocation(position, nodeId);
            });
        }

        void RouteNewOrders()
        {
            var orders = _session.Demand.Orders;
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order.State != OrderState.Pending)
                    continue;

                _session.Hubs.TryAcceptOrder(order);
            }
        }

        void TickOrderPatience(float deltaTime)
        {
            SimLinq.ForEachHub(_session.Hubs.GetHubs(), hub =>
                SimLinq.TickPatienceForOrders(hub.PendingOrders, deltaTime));

            SimLinq.TickPatienceForOrders(_session.Hubs.CityQueue, deltaTime);
        }

        void TickFleetCooldowns(float deltaTime)
        {
            SimLinq.ForEachVehicle(_session.Fleet.GetAllVehicles(), vehicle =>
                vehicle.TickCooldown(deltaTime));
        }

        void RefreshDispatchOrders()
        {
            _session.DispatchOrders.Clear();

            SimLinq.ForEachHub(_session.Hubs.GetHubs(), hub =>
                SimLinq.CollectPendingOrders(_session.DispatchOrders, hub.PendingOrders));

            SimLinq.CollectPendingOrders(_session.DispatchOrders, _session.Hubs.CityQueue);
        }

        void ProcessOrderOutcomes()
        {
            SimLinq.ForEachHub(_session.Hubs.GetHubs(), hub =>
                ProcessOrderList(hub.PendingOrders));

            ProcessOrderList(_session.Hubs.CityQueue);
        }

        void ProcessOrderList(IReadOnlyList<OrderInstance> orders)
        {
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];

                if (order.State == OrderState.Completed && !_processedCompletions.Contains(order.Id))
                {
                    _session.Rating.ApplyJobOutcome(order.RemainingFraction);
                    _session.Economy.OnJobCompleted(order, _session.State.CurrentStars);
                    _orderCompletedChannel?.Raise(new OrderEventPayload(order.Id, order.Module));
                    _processedCompletions.Add(order.Id);
                    continue;
                }

                if (order.State == OrderState.Expired && !_processedExpirations.Contains(order.Id))
                {
                    _session.Rating.ApplyJobOutcome(order.RemainingFraction);
                    _processedExpirations.Add(order.Id);
                }
            }
        }

        void MonitorRunFailure()
        {
            if (_lastPhase != RunPhase.Failed && _session.State.Phase == RunPhase.Failed)
                _runFailedChannel?.Raise();

            _lastPhase = _session.State.Phase;
        }
    }
}
