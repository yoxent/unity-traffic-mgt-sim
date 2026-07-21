using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Hubs;
using UnityEngine;

namespace TrafficSim.Systems
{
    public sealed class OverloadSystem
    {
        readonly RunState _state;
        readonly HubManager _hubManager;
        readonly OverloadDef _def;

        public OverloadSystem(RunState state, HubManager hubManager, OverloadDef def)
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
            var total = 0;

            foreach (var hub in _hubManager.GetHubs())
                total += hub.Capacity;

            return Mathf.Max(0, Mathf.RoundToInt(total * _def.capacityMultiplier));
        }
    }
}
