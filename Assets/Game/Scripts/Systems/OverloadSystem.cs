using TrafficSim.Core;
using TrafficSim.Core.Contracts;
using TrafficSim.Core.Linq;
using TrafficSim.Data;
using UnityEngine;

namespace TrafficSim.Systems
{
    public sealed class OverloadSystem
    {
        readonly RunState _state;
        readonly IHubManager _hubManager;
        readonly OverloadDef _def;

        public OverloadSystem(RunState state, IHubManager hubManager, OverloadDef def)
        {
            _state = state;
            _hubManager = hubManager;
            _def = def;
        }

        public void Tick()
        {
            if (_state.Phase != RunPhase.Playing)
                return;

            if (_hubManager.GetUnassignedOrderCount() > GetCapacityThreshold())
                _state.Phase = RunPhase.Failed;
        }

        int GetCapacityThreshold()
        {
            var total = SimLinq.SumHubCapacity(_hubManager.GetHubs());
            return Mathf.Max(0, Mathf.RoundToInt(total * _def.capacityMultiplier));
        }
    }
}
