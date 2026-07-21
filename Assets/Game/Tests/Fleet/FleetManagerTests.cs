using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Fleet;
using UnityEngine;

namespace TrafficSim.Tests.Fleet
{
    public class FleetManagerTests
    {
        static VehicleDef CreateTestVehicleDef(
            float maxDurability = 10f,
            float durabilityLossPerJob = 5f,
            float cooldownSeconds = 0f,
            float purchaseCost = 100f,
            float repairCost = 10f)
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            def.type = VehicleType.Motorbike;
            def.maxDurability = maxDurability;
            def.durabilityLossPerJob = durabilityLossPerJob;
            def.cooldownSeconds = cooldownSeconds;
            def.purchaseCost = purchaseCost;
            def.repairCost = repairCost;
            def.allowedModules = new[] { ServiceModule.Food };
            def.allowedSizeBands = new[] { JobSizeBand.Small };
            return def;
        }

        [Test]
        public void CompleteJob_DecreasesDurability_AtZeroNotDispatchEligible()
        {
            var def = CreateTestVehicleDef(maxDurability: 10f, durabilityLossPerJob: 5f, cooldownSeconds: 0f);
            var vehicle = new VehicleInstance(1, def, ServiceModule.Food);

            Assert.IsTrue(vehicle.TryAssignOrder(new OrderInstance(100)));
            vehicle.CompleteJob();

            Assert.AreEqual(5f, vehicle.Durability, 0.001f);
            Assert.AreEqual(VehicleState.Idle, vehicle.State);
            Assert.IsTrue(vehicle.IsDispatchEligible);

            Assert.IsTrue(vehicle.TryAssignOrder(new OrderInstance(101)));
            vehicle.CompleteJob();

            Assert.AreEqual(0f, vehicle.Durability, 0.001f);
            Assert.AreEqual(VehicleState.Idle, vehicle.State);
            Assert.IsFalse(vehicle.IsDispatchEligible);
        }

        [Test]
        public void BuyVehicle_DeductsMoneyAndAddsToFleet()
        {
            var def = CreateTestVehicleDef(purchaseCost: 100f);
            var state = new RunState { Money = 150f };
            var manager = new FleetManager(state);

            Assert.IsTrue(manager.BuyVehicle(ServiceModule.Food, def));
            Assert.AreEqual(50f, state.Money, 0.001f);
            Assert.AreEqual(1, manager.GetFleetCount(ServiceModule.Food, VehicleType.Motorbike));
        }

        [Test]
        public void RepairVehicle_RestoresDurabilityWhenAffordable()
        {
            var def = CreateTestVehicleDef(maxDurability: 10f, durabilityLossPerJob: 10f, repairCost: 15f);
            var state = new RunState { Money = 120f };
            var manager = new FleetManager(state);
            manager.BuyVehicle(ServiceModule.Food, def);
            var vehicle = manager.GetVehicles(ServiceModule.Food, VehicleType.Motorbike)[0];

            vehicle.TryAssignOrder(new OrderInstance(1));
            vehicle.CompleteJob();
            Assert.AreEqual(0f, vehicle.Durability, 0.001f);

            Assert.IsTrue(manager.RepairVehicle(vehicle.Id));
            Assert.AreEqual(10f, vehicle.Durability, 0.001f);
            Assert.AreEqual(5f, state.Money, 0.001f);
        }

        [Test]
        public void QueueUpgrade_CostScalesWithFleetCount_DoesNotRefreshDurability()
        {
            var def = CreateTestVehicleDef(
                maxDurability: 10f,
                durabilityLossPerJob: 5f,
                purchaseCost: 100f);
            var state = new RunState { Money = 1000f };
            var queue = new EodActionQueue();
            var manager = new FleetManager(state, queue, baseUpgradeCost: 50f);

            manager.BuyVehicle(ServiceModule.Food, def);
            manager.BuyVehicle(ServiceModule.Food, def);

            var vehicles = manager.GetVehicles(ServiceModule.Food, VehicleType.Motorbike);
            vehicles[0].TryAssignOrder(new OrderInstance(1));
            vehicles[0].CompleteJob();
            var durabilityBefore = vehicles[0].Durability;

            manager.QueueUpgrade(ServiceModule.Food, VehicleType.Motorbike);
            Assert.AreEqual(1, queue.Pending.Count);
            Assert.AreEqual(100f, queue.Pending[0].Cost, 0.001f);

            queue.ApplyAll(state);

            Assert.AreEqual(1, manager.GetUpgradeTier(ServiceModule.Food, VehicleType.Motorbike));
            Assert.AreEqual(durabilityBefore, vehicles[0].Durability, 0.001f);
            Assert.AreEqual(700f, state.Money, 0.001f);
        }

        [Test]
        public void QueueScrap_RemovesIdleVehicleOnApply()
        {
            var def = CreateTestVehicleDef();
            var state = new RunState { Money = 150f };
            var queue = new EodActionQueue();
            var manager = new FleetManager(state, queue);
            Assert.IsTrue(manager.BuyVehicle(ServiceModule.Food, def));
            var vehicle = manager.GetVehicles(ServiceModule.Food, VehicleType.Motorbike)[0];

            manager.QueueScrap(vehicle.Id);
            queue.ApplyAll(state);

            Assert.AreEqual(0, manager.GetFleetCount(ServiceModule.Food, VehicleType.Motorbike));
        }
    }
}
