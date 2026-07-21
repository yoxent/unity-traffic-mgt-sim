using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;

namespace TrafficSim.Fleet
{
    public readonly struct FleetKey : IEquatable<FleetKey>
    {
        public ServiceModule Module { get; }
        public VehicleType Type { get; }

        public FleetKey(ServiceModule module, VehicleType type)
        {
            Module = module;
            Type = type;
        }

        public bool Equals(FleetKey other) => Module == other.Module && Type == other.Type;

        public override bool Equals(object obj) => obj is FleetKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Module, Type);
    }

    public sealed class FleetManager
    {
        readonly RunState _state;
        readonly EodActionQueue _eodQueue;
        readonly float _baseUpgradeCost;
        readonly Dictionary<int, VehicleInstance> _vehiclesById = new();
        readonly Dictionary<FleetKey, List<VehicleInstance>> _fleets = new();
        readonly Dictionary<FleetKey, int> _upgradeTier = new();
        int _nextId = 1;

        public FleetManager(RunState state, EodActionQueue eodQueue = null, float baseUpgradeCost = 50f)
        {
            _state = state;
            _eodQueue = eodQueue ?? new EodActionQueue();
            _baseUpgradeCost = baseUpgradeCost;
        }

        public EodActionQueue EodQueue => _eodQueue;

        public IReadOnlyList<VehicleInstance> GetVehicles(ServiceModule module, VehicleType type)
        {
            var key = new FleetKey(module, type);
            return _fleets.TryGetValue(key, out var fleet) ? fleet : Array.Empty<VehicleInstance>();
        }

        public int GetFleetCount(ServiceModule module, VehicleType type) =>
            GetVehicles(module, type).Count;

        public int GetUpgradeTier(ServiceModule module, VehicleType type)
        {
            var key = new FleetKey(module, type);
            return _upgradeTier.TryGetValue(key, out var tier) ? tier : 0;
        }

        public float GetEffectiveSpeed(VehicleInstance vehicle)
        {
            var tier = GetUpgradeTier(vehicle.Module, vehicle.Def.type);
            return vehicle.Def.speed * (1f + tier * 0.1f);
        }

        public bool BuyVehicle(ServiceModule module, VehicleDef def)
        {
            if (def == null || !CanServeModule(def, module) || _state.Money < def.purchaseCost)
                return false;

            _state.Money -= def.purchaseCost;
            AddVehicle(module, def);
            return true;
        }

        public bool RepairVehicle(int id)
        {
            if (!_vehiclesById.TryGetValue(id, out var vehicle))
                return false;

            if (vehicle.Durability >= vehicle.Def.maxDurability || _state.Money < vehicle.Def.repairCost)
                return false;

            _state.Money -= vehicle.Def.repairCost;
            vehicle.Repair();
            return true;
        }

        public void QueueScrap(int id)
        {
            if (!_vehiclesById.TryGetValue(id, out var vehicle))
                return;

            if (vehicle.State != VehicleState.Idle && vehicle.State != VehicleState.Offline)
                return;

            _eodQueue.Enqueue(new EodAction
            {
                Cost = 0f,
                Apply = _ => RemoveVehicle(id)
            });
        }

        public void QueueUpgrade(ServiceModule module, VehicleType type)
        {
            var key = new FleetKey(module, type);
            if (!_fleets.TryGetValue(key, out var fleet) || fleet.Count == 0)
                return;

            var cost = _baseUpgradeCost * fleet.Count;
            _eodQueue.Enqueue(new EodAction
            {
                Cost = cost,
                Apply = _ => ApplyUpgrade(key)
            });
        }

        void AddVehicle(ServiceModule module, VehicleDef def)
        {
            var vehicle = new VehicleInstance(_nextId++, def, module);
            _vehiclesById[vehicle.Id] = vehicle;

            var key = new FleetKey(module, def.type);
            if (!_fleets.TryGetValue(key, out var fleet))
            {
                fleet = new List<VehicleInstance>();
                _fleets[key] = fleet;
            }

            fleet.Add(vehicle);
        }

        void RemoveVehicle(int id)
        {
            if (!_vehiclesById.TryGetValue(id, out var vehicle))
                return;

            _vehiclesById.Remove(id);
            var key = new FleetKey(vehicle.Module, vehicle.Def.type);
            if (_fleets.TryGetValue(key, out var fleet))
                fleet.Remove(vehicle);
        }

        void ApplyUpgrade(FleetKey key)
        {
            _upgradeTier.TryGetValue(key, out var tier);
            _upgradeTier[key] = tier + 1;
        }

        static bool CanServeModule(VehicleDef def, ServiceModule module)
        {
            if (def.allowedModules == null)
                return false;

            for (var i = 0; i < def.allowedModules.Length; i++)
            {
                if (def.allowedModules[i] == module)
                    return true;
            }

            return false;
        }
    }
}
