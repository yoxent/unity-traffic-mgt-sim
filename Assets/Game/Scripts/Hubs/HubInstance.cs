using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;

namespace TrafficSim.Hubs
{
    public sealed class HubInstance
    {
        readonly List<OrderInstance> _pendingOrders = new();

        public int Id { get; }
        public HubDef Def { get; }
        public int SlotId { get; private set; }
        public HubState State { get; private set; }
        public int? PendingRelocateSlotId { get; private set; }

        public IReadOnlyList<OrderInstance> PendingOrders => _pendingOrders;
        public int Capacity => Def.capacity;
        public bool AcceptsNewOrders => State == HubState.Active;

        public HubInstance(int id, HubDef def, int slotId)
        {
            Id = id;
            Def = def;
            SlotId = slotId;
            State = HubState.Active;
        }

        public int PendingOrderCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _pendingOrders.Count; i++)
                {
                    if (_pendingOrders[i].State == OrderState.Pending)
                        count++;
                }

                return count;
            }
        }

        public bool HasPendingOrders => PendingOrderCount > 0;

        public void AddOrder(OrderInstance order) => _pendingOrders.Add(order);

        public void RemoveOrder(OrderInstance order) => _pendingOrders.Remove(order);

        public void BeginClosing() => State = HubState.Closing;

        public void Activate()
        {
            State = HubState.Active;
            PendingRelocateSlotId = null;
        }

        public void SetSlot(int slotId) => SlotId = slotId;

        public void SetPendingRelocateSlot(int slotId) => PendingRelocateSlotId = slotId;

        public void ClearPendingRelocate() => PendingRelocateSlotId = null;
    }
}
