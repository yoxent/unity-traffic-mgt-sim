using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Systems;
using UnityEngine;

namespace TrafficSim.Tests.Systems
{
    // Edit Mode tests for RatingSystem and RunState streak logic.
    public class RatingSystemTests
    {
        static RatingDef CreateDefaultRatingDef()
        {
            return ScriptableObject.CreateInstance<RatingDef>();
        }

        [Test]
        public void ApplyJobOutcome_80PercentRemaining_IncreasesStars()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { CurrentStars = 3f };
            var system = new RatingSystem(state, def);
            system.ApplyJobOutcome(0.85f);
            Assert.Greater(state.CurrentStars, 3f);
        }

        [Test]
        public void ApplyJobOutcome_ClampedBetweenOneAndFive()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { CurrentStars = 1f };
            var system = new RatingSystem(state, def);
            system.ApplyJobOutcome(0f);
            Assert.AreEqual(1f, state.CurrentStars);

            state.CurrentStars = 5f;
            system.ApplyJobOutcome(1f);
            Assert.AreEqual(5f, state.CurrentStars);
        }

        [Test]
        public void SnapshotEndOfDay_OneStar_IncrementsStreak()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { CurrentStars = 1.4f };
            var system = new RatingSystem(state, def);
            system.SnapshotEndOfDay();
            Assert.AreEqual(1, state.ConsecutiveOneStarDays);
        }

        [Test]
        public void SnapshotEndOfDay_TwoStars_ResetsStreak()
        {
            var def = CreateDefaultRatingDef();
            var state = new RunState { CurrentStars = 2f, ConsecutiveOneStarDays = 2 };
            var system = new RatingSystem(state, def);
            system.SnapshotEndOfDay();
            Assert.AreEqual(0, state.ConsecutiveOneStarDays);
        }

        [Test]
        public void ShouldFailStreak_WhenConsecutiveOneStarDaysReachThreshold()
        {
            var def = CreateDefaultRatingDef();
            def.streakFailDays = 3;
            var state = new RunState { ConsecutiveOneStarDays = 3 };
            var system = new RatingSystem(state, def);
            Assert.IsTrue(system.ShouldFailStreak(def));
        }

        [Test]
        public void ShouldFailStreak_BelowThreshold_ReturnsFalse()
        {
            var def = CreateDefaultRatingDef();
            def.streakFailDays = 3;
            var state = new RunState { ConsecutiveOneStarDays = 2 };
            var system = new RatingSystem(state, def);
            Assert.IsFalse(system.ShouldFailStreak(def));
        }
    }
}
