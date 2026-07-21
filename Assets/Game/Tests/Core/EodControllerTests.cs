using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Systems;
using UnityEngine;

namespace TrafficSim.Tests.Core
{
    public class EodControllerTests
    {
        static RatingDef CreateDefaultRatingDef()
        {
            var def = ScriptableObject.CreateInstance<RatingDef>();
            def.streakFailDays = 3;
            return def;
        }

        static (RunState state, RatingSystem rating, DayClock clock, EodController controller) CreateController()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { CurrentStars = 1.4f };
            var rating = new RatingSystem(state, def);
            var clock = new DayClock(dayLengthSeconds: 300f);
            var controller = new EodController(state, rating, def, clock);
            return (state, rating, clock, controller);
        }

        [Test]
        public void BeginEod_ThreeConsecutiveOneStarSnapshots_SetsPhaseFailed()
        {
            var (state, _, _, controller) = CreateController();

            controller.BeginEod(skipIntervention: true);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(1, state.ConsecutiveOneStarDays);

            state.CurrentStars = 1.2f;
            controller.BeginEod(skipIntervention: true);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(2, state.ConsecutiveOneStarDays);

            state.CurrentStars = 1f;
            controller.BeginEod(skipIntervention: true);
            Assert.AreEqual(RunPhase.Failed, state.Phase);
            Assert.AreEqual(3, state.ConsecutiveOneStarDays);
        }

        [Test]
        public void ApplyQueue_SkipsUnaffordableActions()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { Money = 50f };
            var rating = new RatingSystem(state, def);
            var clock = new DayClock(300f);
            var queue = new EodActionQueue();
            var applied = false;

            queue.Enqueue(new EodAction
            {
                Cost = 100f,
                Apply = _ => applied = true
            });

            var controller = new EodController(state, rating, def, clock, queue);
            controller.ApplyQueue();

            Assert.IsFalse(applied);
            Assert.AreEqual(50f, state.Money);
            Assert.AreEqual(1, queue.Pending.Count);
        }

        [Test]
        public void AdvanceDay_ResetsClockAndIncrementsDayIndex()
        {
            var (state, _, clock, controller) = CreateController();
            clock.Advance(300f);
            state.Phase = RunPhase.EodIntervention;

            controller.AdvanceDay();

            Assert.AreEqual(1, state.DayIndex);
            Assert.AreEqual(0f, clock.DayFraction);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
        }
    }
}
