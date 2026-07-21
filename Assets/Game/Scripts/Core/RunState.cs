using System.Collections.Generic;

namespace TrafficSim.Core
{
    public sealed class RunState
    {
        public int DayIndex;
        public float CurrentStars = 3f;
        public int ConsecutiveOneStarDays;
        public float Money;
        public float CumulativeProfit;
        public float PeakProfit;
        public int SuccessfulJobs;
        public RunPhase Phase = RunPhase.Playing;
        public HashSet<ServiceModule> UnlockedModules = new();
    }
}
