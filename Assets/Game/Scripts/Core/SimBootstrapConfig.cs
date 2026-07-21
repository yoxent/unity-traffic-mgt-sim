using TrafficSim.Data;
using TrafficSim.Events;

namespace TrafficSim.Core
{
    public sealed class SimBootstrapConfig
    {
        public DemandWaveDef DemandWaveDef;
        public OverloadDef OverloadDef;
        public RatingDef RatingDef;
        public ServiceModuleDef[] ModuleDefs;
        public HubDef[] HubDefs;
        public VehicleDef[] VehicleDefs;
        public float DayLengthSeconds = 300f;
        public float StartingMoney = 500f;
        public OrderEventChannel OrderAssignedChannel;
    }
}
