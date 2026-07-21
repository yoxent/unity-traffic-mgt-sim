using TrafficSim.Core;

namespace TrafficSim.Demand
{
    public readonly struct DemandCheckpoint
    {
        public float DayFraction { get; }
        public ServiceModule Module { get; }
        public JobSizeBand SizeBand { get; }
        public int Count { get; }

        public DemandCheckpoint(float dayFraction, ServiceModule module, JobSizeBand sizeBand, int count)
        {
            DayFraction = dayFraction;
            Module = module;
            SizeBand = sizeBand;
            Count = count;
        }
    }
}
