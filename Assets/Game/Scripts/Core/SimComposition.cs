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
    public static class SimComposition
    {
        public static SimSession Build(SimBootstrapConfig config, RoadGraph graph)
        {
            var moduleDefLookup = BuildModuleLookup(config.ModuleDefs);
            var hubDefLookup = BuildHubLookup(config.HubDefs);
            var vehicleDefLookup = BuildVehicleLookup(config.VehicleDefs);

            var state = new RunState { Money = config.StartingMoney };
            var eodQueue = new EodActionQueue();
            var clock = new DayClock(config.DayLengthSeconds);
            var rating = new RatingSystem(state, config.RatingDef);
            var economy = new EconomySystem(state, config.RatingDef);
            var fleet = new FleetManager(state, eodQueue);
            var hubManager = new HubManager(state, eodQueue);
            var demand = new DemandSpawner(
                config.DemandWaveDef,
                config.DayLengthSeconds,
                moduleDefLookup,
                graph);
            var dispatchOrders = new List<OrderInstance>();
            var dispatch = new DispatchService(
                fleet,
                graph,
                dispatchOrders,
                config.OrderAssignedChannel);
            var overload = new OverloadSystem(state, hubManager, config.OverloadDef);
            var activeHubDefs = new List<HubDef>();
            var eod = new EodController(
                state,
                rating,
                config.RatingDef,
                clock,
                eodQueue,
                economy,
                activeHubDefs);

            return new SimSession
            {
                State = state,
                Clock = clock,
                EodQueue = eodQueue,
                Eod = eod,
                Rating = rating,
                Economy = economy,
                Fleet = fleet,
                Hubs = hubManager,
                Demand = demand,
                Dispatch = dispatch,
                DispatchOrders = dispatchOrders,
                Overload = overload,
                Graph = graph,
                ActiveHubDefs = activeHubDefs,
                ModuleDefLookup = moduleDefLookup,
                HubDefLookup = hubDefLookup,
                VehicleDefLookup = vehicleDefLookup
            };
        }

        static Dictionary<ServiceModule, ServiceModuleDef> BuildModuleLookup(ServiceModuleDef[] defs)
        {
            var lookup = new Dictionary<ServiceModule, ServiceModuleDef>();
            if (defs == null)
                return lookup;

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def != null)
                    lookup[def.module] = def;
            }

            return lookup;
        }

        static Dictionary<ServiceModule, HubDef> BuildHubLookup(HubDef[] defs)
        {
            var lookup = new Dictionary<ServiceModule, HubDef>();
            if (defs == null)
                return lookup;

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def != null)
                    lookup[def.module] = def;
            }

            return lookup;
        }

        static Dictionary<VehicleType, VehicleDef> BuildVehicleLookup(VehicleDef[] defs)
        {
            var lookup = new Dictionary<VehicleType, VehicleDef>();
            if (defs == null)
                return lookup;

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def != null)
                    lookup[def.type] = def;
            }

            return lookup;
        }
    }
}
