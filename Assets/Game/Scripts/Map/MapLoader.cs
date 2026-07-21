using TrafficSim.Data;

namespace TrafficSim.Map
{
    public static class MapLoader
    {
        public static RoadGraph Load(MapSkeleton skeleton)
        {
            return RoadGraph.BuildLineGraph(skeleton.roadNodePositions);
        }
    }
}
