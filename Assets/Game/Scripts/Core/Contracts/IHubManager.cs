using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Hubs;

namespace TrafficSim.Core.Contracts
{
    public interface IHubManager
    {
        IReadOnlyList<OrderInstance> CityQueue { get; }
        IReadOnlyCollection<HubInstance> GetHubs();
        int GetUnassignedOrderCount();
        bool PlaceHub(HubDef def, int slotId);
        bool TryAcceptOrder(OrderInstance order);
        void Tick(float deltaTime);
        void UnlockSlot(int slotId);
    }
}
