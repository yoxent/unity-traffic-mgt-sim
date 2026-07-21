using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Hubs;
using UnityEngine;

namespace TrafficSim.Tests.Hubs
{
    public class HubManagerTests
    {
        static HubDef CreateFoodHubDef(int capacity = 3, float relocateCost = 50f)
        {
            var def = ScriptableObject.CreateInstance<HubDef>();
            def.module = ServiceModule.Food;
            def.capacity = capacity;
            def.dailyUpkeep = 10f;
            def.relocateCost = relocateCost;
            return def;
        }

        static HubManager CreateManager(out RunState state, out EodActionQueue queue, params int[] unlockedSlots)
        {
            state = new RunState { Money = 1000f };
            queue = new EodActionQueue();
            return new HubManager(state, queue, unlockedSlots);
        }

        [Test]
        public void PlaceHub_OnlyOnUnlockedEmptySlot()
        {
            var manager = CreateManager(out _, out _, 0);

            Assert.IsTrue(manager.PlaceHub(CreateFoodHubDef(), slotId: 0));
            Assert.IsFalse(manager.PlaceHub(CreateFoodHubDef(), slotId: 0));
            Assert.IsFalse(manager.PlaceHub(CreateFoodHubDef(), slotId: 1));
        }

        [Test]
        public void SoleHubClosing_NewDemandGoesToCityQueue_PatienceTicks()
        {
            var manager = CreateManager(out var state, out var queue, 0, 1);
            var hubDef = CreateFoodHubDef();

            Assert.IsTrue(manager.PlaceHub(hubDef, slotId: 0));
            manager.TryGetHub(1, out var hub);

            manager.QueueRelocate(hub.Id, newSlotId: 1);
            Assert.AreEqual(1, queue.Pending.Count);
            queue.ApplyAll(state);

            Assert.AreEqual(HubState.Closing, hub.State);
            Assert.AreEqual(hub.Id, manager.ClosingHubId);

            var order = new OrderInstance(
                1,
                ServiceModule.Food,
                JobSizeBand.Small,
                0,
                1,
                patienceTotal: 100f,
                graceTotal: 0f);

            Assert.IsTrue(manager.TryAcceptOrder(order));
            Assert.AreEqual(1, manager.CityQueue.Count);
            Assert.AreEqual(0, hub.PendingOrderCount);

            var patienceBefore = manager.CityQueue[0].PatienceRemaining;
            manager.Tick(5f);
            Assert.Less(manager.CityQueue[0].PatienceRemaining, patienceBefore);
        }

        [Test]
        public void CancelClose_RestoresCityQueueToHub()
        {
            var manager = CreateManager(out var state, out var queue, 0, 1);
            var hubDef = CreateFoodHubDef();

            Assert.IsTrue(manager.PlaceHub(hubDef, slotId: 0));
            manager.TryGetHub(1, out var hub);

            manager.QueueRelocate(hub.Id, newSlotId: 1);
            queue.ApplyAll(state);

            var order = new OrderInstance(1, ServiceModule.Food, JobSizeBand.Small, 0, 1, 100f, 0f);
            Assert.IsTrue(manager.TryAcceptOrder(order));
            Assert.AreEqual(1, manager.CityQueue.Count);

            manager.CancelClose(hub.Id);

            Assert.AreEqual(HubState.Active, hub.State);
            Assert.IsNull(manager.ClosingHubId);
            Assert.AreEqual(0, manager.CityQueue.Count);
            Assert.AreEqual(1, hub.PendingOrderCount);
        }

        [Test]
        public void QueueRelocate_OnlyOneGlobalClosing()
        {
            var manager = CreateManager(out var state, out var queue, 0, 1, 2);
            var foodHub = CreateFoodHubDef();

            Assert.IsTrue(manager.PlaceHub(foodHub, slotId: 0));

            var carHub = ScriptableObject.CreateInstance<HubDef>();
            carHub.module = ServiceModule.Car;
            carHub.relocateCost = 50f;
            Assert.IsTrue(manager.PlaceHub(carHub, slotId: 1));

            manager.TryGetHub(1, out var firstHub);
            manager.TryGetHub(2, out var secondHub);

            manager.QueueRelocate(firstHub.Id, newSlotId: 2);
            queue.ApplyAll(state);
            Assert.AreEqual(firstHub.Id, manager.ClosingHubId);

            manager.QueueRelocate(secondHub.Id, newSlotId: 0);
            queue.ApplyAll(state);
            Assert.AreEqual(firstHub.Id, manager.ClosingHubId);
            Assert.AreEqual(HubState.Active, secondHub.State);
        }

        [Test]
        public void QueueRelocate_CompletesWhenPendingOrdersDrain()
        {
            var manager = CreateManager(out var state, out var queue, 0, 1);
            var hubDef = CreateFoodHubDef();

            Assert.IsTrue(manager.PlaceHub(hubDef, slotId: 0));
            manager.TryGetHub(1, out var hub);

            var pending = new OrderInstance(1, ServiceModule.Food, JobSizeBand.Small, 0, 1, 100f, 0f);
            hub.AddOrder(pending);

            manager.QueueRelocate(hub.Id, newSlotId: 1);
            queue.ApplyAll(state);

            manager.Tick(0f);
            Assert.AreEqual(HubState.Closing, hub.State);
            Assert.AreEqual(0, hub.SlotId);

            pending.MarkAssigned();
            manager.Tick(0f);

            Assert.AreEqual(HubState.Active, hub.State);
            Assert.AreEqual(1, hub.SlotId);
            Assert.IsNull(manager.ClosingHubId);
        }

        [Test]
        public void QueueRelocate_RaisesTransferWarningWhenAlternateHubExists()
        {
            var manager = CreateManager(out var state, out var queue, 0, 1, 2);
            var foodHub = CreateFoodHubDef();

            Assert.IsTrue(manager.PlaceHub(foodHub, slotId: 0));
            Assert.IsTrue(manager.PlaceHub(foodHub, slotId: 1));

            manager.TryGetHub(1, out var closingHub);

            var warningRaised = false;
            manager.TransferWarning += (hubId, alternateCount) =>
            {
                warningRaised = true;
                Assert.AreEqual(closingHub.Id, hubId);
                Assert.AreEqual(1, alternateCount);
            };

            manager.QueueRelocate(closingHub.Id, newSlotId: 2);
            queue.ApplyAll(state);

            Assert.IsTrue(warningRaised);
        }
    }
}
