using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Hubs;
using TrafficSim.Systems;
using UnityEngine;

namespace TrafficSim.Tests.Systems
{
    public class OverloadSystemTests
    {
        static OverloadDef CreateDefaultOverloadDef() =>
            ScriptableObject.CreateInstance<OverloadDef>();

        static HubDef CreateFoodHubDef(int capacity = 3)
        {
            var def = ScriptableObject.CreateInstance<HubDef>();
            def.module = ServiceModule.Food;
            def.capacity = capacity;
            return def;
        }

        static OrderInstance CreatePendingOrder(int id) =>
            new(id, ServiceModule.Food, JobSizeBand.Small, 0, 1, 100f, 0f);

        [Test]
        public void Tick_OrdersExceedCapacity_SetsPhaseFailed()
        {
            var state = new RunState();
            var hubManager = new HubManager(state, initialUnlockedSlots: new[] { 0, 1 });
            var overloadDef = CreateDefaultOverloadDef();

            Assert.IsTrue(hubManager.PlaceHub(CreateFoodHubDef(capacity: 3), slotId: 0));
            hubManager.TryGetHub(1, out var hub);

            for (var i = 0; i < 4; i++)
                hub.AddOrder(CreatePendingOrder(i + 1));

            var system = new OverloadSystem(state, hubManager, overloadDef);
            system.Tick();

            Assert.AreEqual(RunPhase.Failed, state.Phase);
        }

        [Test]
        public void Tick_OrdersAtCapacity_RemainsPlaying()
        {
            var state = new RunState();
            var hubManager = new HubManager(state, initialUnlockedSlots: new[] { 0 });
            var overloadDef = CreateDefaultOverloadDef();

            Assert.IsTrue(hubManager.PlaceHub(CreateFoodHubDef(capacity: 3), slotId: 0));
            hubManager.TryGetHub(1, out var hub);

            for (var i = 0; i < 3; i++)
                hub.AddOrder(CreatePendingOrder(i + 1));

            var system = new OverloadSystem(state, hubManager, overloadDef);
            system.Tick();

            Assert.AreEqual(RunPhase.Playing, state.Phase);
        }

        [Test]
        public void Tick_CityQueueOrders_CountTowardOverload()
        {
            var state = new RunState();
            var queue = new EodActionQueue();
            var hubManager = new HubManager(state, queue, new[] { 0, 1 });
            var overloadDef = CreateDefaultOverloadDef();

            Assert.IsTrue(hubManager.PlaceHub(CreateFoodHubDef(capacity: 2), slotId: 0));
            hubManager.TryGetHub(1, out var hub);

            hub.AddOrder(CreatePendingOrder(1));
            hub.AddOrder(CreatePendingOrder(2));

            hubManager.QueueRelocate(hub.Id, newSlotId: 1);
            queue.ApplyAll(state);

            Assert.IsTrue(hubManager.TryAcceptOrder(CreatePendingOrder(3)));

            var system = new OverloadSystem(state, hubManager, overloadDef);
            system.Tick();

            Assert.AreEqual(RunPhase.Failed, state.Phase);
        }
    }
}
