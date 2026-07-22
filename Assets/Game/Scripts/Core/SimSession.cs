using System.Collections.Generic;
using TrafficSim.Core.Contracts;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Dispatch;
using TrafficSim.Fleet;
using TrafficSim.Hubs;
using TrafficSim.Map;
using TrafficSim.Systems;

namespace TrafficSim.Core
{
    public sealed class SimSession
    {
        public RunState State { get; set; }
        public DayClock Clock { get; set; }
        public EodActionQueue EodQueue { get; set; }
        public EodController Eod { get; set; }
        public RatingSystem Rating { get; set; }
        public EconomySystem Economy { get; set; }
        public FleetManager Fleet { get; set; }
        public IHubManager Hubs { get; set; }
        public IDemandSource Demand { get; set; }
        public IDispatchService Dispatch { get; set; }
        public IList<OrderInstance> DispatchOrders { get; set; }
        public OverloadSystem Overload { get; set; }
        public RoadGraph Graph { get; set; }
        public HouseRegistry Houses { get; set; }
        public List<HubDef> ActiveHubDefs { get; set; }
        public Dictionary<ServiceModule, ServiceModuleDef> ModuleDefLookup { get; set; }
        public Dictionary<ServiceModule, HubDef> HubDefLookup { get; set; }
        public Dictionary<VehicleType, VehicleDef> VehicleDefLookup { get; set; }
    }
}
