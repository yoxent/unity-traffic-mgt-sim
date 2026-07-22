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
                SimLog.Warn("Module", $"Init skipped for {module} — missing module/hub/vehicle def.");
                return;
            }

            var slotId = _initializedModules.Count;
            if (slotId > 0)
                _session.Hubs.UnlockSlot(slotId);

            if (!_session.Hubs.PlaceHub(hubDef, slotId))
            {
                SimLog.Warn("Module", $"Init failed for {module} — could not place hub on slot {slotId}.");
                return;
            }

            _session.ActiveHubDefs.Add(hubDef);

            var bought = 0;
            for (var i = 0; i < moduleDef.starterVehicleCount; i++)
            {
                if (_session.Fleet.BuyVehicle(module, vehicleDef))
                    bought++;
            }

            PositionFleetAtSlot(module, slotId);
            _initializedModules.Add(module);
            SimLog.ModuleInfo(
                $"Initialized {module} hubSlot={slotId} starters={bought}/{moduleDef.starterVehicleCount} type={vehicleDef.type} money={_session.State.Money:F0}");
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
                    var starsBefore = _session.State.CurrentStars;
                    var moneyBefore = _session.State.Money;
                    _session.Rating.ApplyJobOutcome(order.RemainingFraction);
                    _session.Economy.OnJobCompleted(order, _session.State.CurrentStars);
                    _orderCompletedChannel?.Raise(new OrderEventPayload(order.Id, order.Module));
                    _processedCompletions.Add(order.Id);
                    SimLog.EconomyInfo(
                        $"Order {order.Id} ({order.Module}) completed rem={order.RemainingFraction:P0} " +
                        $"stars {starsBefore:F1}→{_session.State.CurrentStars:F1} money {moneyBefore:F0}→{_session.State.Money:F0}");
                    continue;
                }

                if (order.State == OrderState.Expired && !_processedExpirations.Contains(order.Id))
                {
                    var starsBefore = _session.State.CurrentStars;
                    _session.Rating.ApplyJobOutcome(order.RemainingFraction);
                    _processedExpirations.Add(order.Id);
                    SimLog.RatingInfo(
                        $"Order {order.Id} ({order.Module}) expired rem={order.RemainingFraction:P0} " +
                        $"stars {starsBefore:F1}→{_session.State.CurrentStars:F1}");
                }
            }
        }

        void MonitorRunFailure()
        {
            if (_lastPhase != RunPhase.Failed && _session.State.Phase == RunPhase.Failed)
            {
                SimLog.PhaseInfo(
                    $"RUN FAILED day={_session.State.DayIndex} stars={_session.State.CurrentStars:F1} " +
                    $"money={_session.State.Money:F0} pending={_session.Hubs.GetUnassignedOrderCount()}");
                _runFailedChannel?.Raise();
            }

            if (_lastPhase != _session.State.Phase)
                SimLog.PhaseInfo($"Phase {_lastPhase} → {_session.State.Phase}");

            _lastPhase = _session.State.Phase;
        }
    }
}
