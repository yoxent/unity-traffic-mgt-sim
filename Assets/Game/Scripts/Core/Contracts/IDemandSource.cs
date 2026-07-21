using System.Collections.Generic;
using TrafficSim.Demand;

namespace TrafficSim.Core.Contracts
{
    public interface IDemandSource
    {
        float DayFraction { get; }
        IReadOnlyList<OrderInstance> Orders { get; }
        IReadOnlyList<DemandCheckpoint> GetUpcomingCheckpoints(int maxCount);
        void Tick(float dayFraction);
    }
}
