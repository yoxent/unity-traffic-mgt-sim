using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Dispatch;
using UnityEngine;

namespace TrafficSim.Fleet
{
    public sealed class VehicleInstance
    {
        public int Id { get; }
        public VehicleDef Def { get; }
        public ServiceModule Module { get; }
        public VehicleState State { get; private set; }
        public float Durability { get; private set; }
        public float CooldownRemaining { get; private set; }
        public int? CurrentOrderId { get; private set; }
        public int CurrentNodeId { get; private set; }
        public Vector3 Position { get; private set; }
        public VehiclePathAgent PathAgent { get; } = new();

        public VehicleInstance(int id, VehicleDef def, ServiceModule module)
        {
            Id = id;
            Def = def;
            Module = module;
            State = VehicleState.Idle;
            Durability = def.maxDurability;
        }

        public bool IsDispatchEligible =>
            State == VehicleState.Idle &&
            Durability > 0f &&
            CooldownRemaining <= 0f &&
            CurrentOrderId == null;

        public bool TryAssignOrder(OrderInstance order)
        {
            if (!IsDispatchEligible || order == null)
                return false;

            CurrentOrderId = order.Id;
            State = VehicleState.EnRoute;
            return true;
        }

        public void CompleteJob()
        {
            if (State != VehicleState.EnRoute)
                return;

            Durability = Mathf.Max(0f, Durability - Def.durabilityLossPerJob);
            CurrentOrderId = null;
            CooldownRemaining = Def.cooldownSeconds;

            if (CooldownRemaining <= 0f)
                State = VehicleState.Idle;
            else
                State = VehicleState.Cooldown;
        }

        public void TickCooldown(float deltaTime)
        {
            if (CooldownRemaining <= 0f)
                return;

            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            if (CooldownRemaining <= 0f && State == VehicleState.Cooldown)
                State = VehicleState.Idle;
        }

        public void Repair() => Durability = Def.maxDurability;

        public void MarkOffline() => State = VehicleState.Offline;

        public void SetLocation(Vector3 position, int nodeId)
        {
            Position = position;
            CurrentNodeId = nodeId;
        }
    }
}
