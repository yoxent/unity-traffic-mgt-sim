using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Core.Contracts;
using TrafficSim.Data;
using TrafficSim.Demand;

namespace TrafficSim.Hubs
{
    public sealed class HubManager : IHubManager
    {
        readonly RunState _state;
        readonly EodActionQueue _eodQueue;
        readonly HashSet<int> _unlockedSlots = new();
        readonly Dictionary<int, HubInstance> _hubsById = new();
        readonly Dictionary<int, int> _slotToHubId = new();
        readonly List<OrderInstance> _cityQueue = new();
        readonly HashSet<int> _acceptedOrderIds = new();
        readonly Func<int, int> _resolvePickupNodeFromSlot;
        int _nextHubId = 1;
        int? _closingHubId;

        public HubManager(
            RunState state,
            EodActionQueue eodQueue = null,
            IEnumerable<int> initialUnlockedSlots = null,
            Func<int, int> resolvePickupNodeFromSlot = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _eodQueue = eodQueue ?? new EodActionQueue();
            _resolvePickupNodeFromSlot = resolvePickupNodeFromSlot;

            if (initialUnlockedSlots != null)
            {
                foreach (var slotId in initialUnlockedSlots)
                    _unlockedSlots.Add(slotId);
            }
            else
            {
                _unlockedSlots.Add(0);
            }
        }

        public EodActionQueue EodQueue => _eodQueue;

        public IReadOnlyList<OrderInstance> CityQueue => _cityQueue;

        public int? ClosingHubId => _closingHubId;

        public event Action<int, int> TransferWarning;

        public bool IsSlotUnlocked(int slotId) => _unlockedSlots.Contains(slotId);

        public bool IsSlotOccupied(int slotId) => _slotToHubId.ContainsKey(slotId);

        public void UnlockSlot(int slotId) => _unlockedSlots.Add(slotId);

        public IReadOnlyCollection<HubInstance> GetHubs() => _hubsById.Values;

        public bool TryGetHub(int hubId, out HubInstance hub) => _hubsById.TryGetValue(hubId, out hub);

        public bool PlaceHub(HubDef def, int slotId)
        {
            if (def == null || !IsSlotUnlocked(slotId) || IsSlotOccupied(slotId))
            {
                SimLog.Warn(
                    "Hub",
                    $"PlaceHub failed module={def?.module} slot={slotId} " +
                    $"unlocked={def != null && IsSlotUnlocked(slotId)} occupied={def != null && IsSlotOccupied(slotId)}");
                return false;
            }

            var hub = new HubInstance(_nextHubId++, def, slotId);
            _hubsById[hub.Id] = hub;
            _slotToHubId[slotId] = hub.Id;
            SimLog.HubInfo($"Placed hub {hub.Id} ({def.module}) on slot {slotId}");
            return true;
        }

        public void QueueRelocate(int hubId, int newSlotId)
        {
            if (!_hubsById.TryGetValue(hubId, out var hub))
                return;

            if (_closingHubId.HasValue || hub.State != HubState.Active)
                return;

            if (!IsSlotUnlocked(newSlotId) || IsSlotOccupied(newSlotId) || newSlotId == hub.SlotId)
                return;

            var alternateHubCount = CountActiveHubsForModule(hub.Def.module, excludingHubId: hubId);
            var capturedHubId = hubId;
            var capturedTargetSlot = newSlotId;

            _eodQueue.Enqueue(new EodAction
            {
                Cost = hub.Def.relocateCost,
                Apply = _ =>
                {
                    if (!_hubsById.TryGetValue(capturedHubId, out var closingHub))
                        return;

                    if (_closingHubId.HasValue || closingHub.State != HubState.Active)
                        return;

                    closingHub.BeginClosing();
                    closingHub.SetPendingRelocateSlot(capturedTargetSlot);
                    _closingHubId = capturedHubId;

                    if (alternateHubCount > 0)
                        TransferWarning?.Invoke(capturedHubId, alternateHubCount);
                }
            });
        }

        public void CancelClose(int hubId)
        {
            if (!_hubsById.TryGetValue(hubId, out var hub) || hub.State != HubState.Closing)
                return;

            hub.Activate();
            _closingHubId = null;

            for (var i = _cityQueue.Count - 1; i >= 0; i--)
            {
                var order = _cityQueue[i];
                if (order.Module != hub.Def.module || order.State != OrderState.Pending)
                    continue;

                _cityQueue.RemoveAt(i);
                hub.AddOrder(order);
            }
        }

        public bool TryAcceptOrder(OrderInstance order)
        {
            if (order == null || order.State != OrderState.Pending)
                return false;

            if (_acceptedOrderIds.Contains(order.Id))
                return true;

            var targetHub = FindAcceptingHub(order.Module, out var fromClosingHub);
            if (targetHub != null)
            {
                if (fromClosingHub)
                    TransferWarning?.Invoke(_closingHubId.Value, 1);

                AssignPickupNode(order, targetHub.SlotId);
                targetHub.AddOrder(order);
                _acceptedOrderIds.Add(order.Id);
                SimLog.HubInfo(
                    $"Accepted order {order.Id} ({order.Module}) → hub {targetHub.Id} house={order.DestinationHouseId} " +
                    $"route {order.PickupNode}→{order.DropoffNode}" +
                    (fromClosingHub ? " (from closing)" : string.Empty));
                return true;
            }

            if (_closingHubId.HasValue)
            {
                if (_hubsById.TryGetValue(_closingHubId.Value, out var closingHub))
                    AssignPickupNode(order, closingHub.SlotId);

                _cityQueue.Add(order);
                _acceptedOrderIds.Add(order.Id);
                SimLog.HubInfo(
                    $"Accepted order {order.Id} ({order.Module}) → city queue (hub closing) house={order.DestinationHouseId}");
                return true;
            }

            SimLog.HubInfo($"Rejected order {order.Id} ({order.Module}) — no accepting hub");
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                for (var i = 0; i < _cityQueue.Count; i++)
                    _cityQueue[i].TickPatience(deltaTime);
            }

            if (!_closingHubId.HasValue)
                return;

            if (!_hubsById.TryGetValue(_closingHubId.Value, out var hub))
                return;

            if (hub.State != HubState.Closing || hub.HasPendingOrders)
                return;

            CompleteRelocate(hub);
        }

        public int GetUnassignedOrderCount()
        {
            var count = 0;

            foreach (var hub in _hubsById.Values)
                count += hub.PendingOrderCount;

            for (var i = 0; i < _cityQueue.Count; i++)
            {
                if (_cityQueue[i].State == OrderState.Pending)
                    count++;
            }

            return count;
        }

        void AssignPickupNode(OrderInstance order, int slotId)
        {
            if (_resolvePickupNodeFromSlot == null)
                return;

            order.SetPickupNode(_resolvePickupNodeFromSlot(slotId));
        }

        HubInstance FindAcceptingHub(ServiceModule module, out bool fromClosingHub)
        {
            fromClosingHub = false;
            HubInstance fallback = null;

            foreach (var hub in _hubsById.Values)
            {
                if (hub.Def.module != module || !hub.AcceptsNewOrders)
                    continue;

                fallback = hub;
            }

            if (fallback != null)
                return fallback;

            if (!_closingHubId.HasValue ||
                !_hubsById.TryGetValue(_closingHubId.Value, out var closingHub) ||
                closingHub.Def.module != module)
            {
                return null;
            }

            fromClosingHub = true;
            return null;
        }

        int CountActiveHubsForModule(ServiceModule module, int excludingHubId)
        {
            var count = 0;

            foreach (var hub in _hubsById.Values)
            {
                if (hub.Id == excludingHubId || hub.Def.module != module || !hub.AcceptsNewOrders)
                    continue;

                count++;
            }

            return count;
        }

        void CompleteRelocate(HubInstance hub)
        {
            if (hub.PendingRelocateSlotId.HasValue)
            {
                var newSlotId = hub.PendingRelocateSlotId.Value;
                _slotToHubId.Remove(hub.SlotId);
                hub.SetSlot(newSlotId);
                _slotToHubId[newSlotId] = hub.Id;
                hub.ClearPendingRelocate();
            }

            hub.Activate();
            _closingHubId = null;
        }
    }
}
