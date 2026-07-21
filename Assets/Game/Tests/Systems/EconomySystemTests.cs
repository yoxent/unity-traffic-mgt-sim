using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Systems;
using UnityEngine;

namespace TrafficSim.Tests.Systems
{
    public class EconomySystemTests
    {
        const float BaseFare = 10f;
        const float TipPerStar = 5f;

        static RatingDef CreateDefaultRatingDef()
        {
            var def = ScriptableObject.CreateInstance<RatingDef>();
            def.minStarsForTips = 3;
            return def;
        }

        static EconomySystem CreateSystem(RunState state, RatingDef ratingDef = null) =>
            new(state, ratingDef ?? CreateDefaultRatingDef(), BaseFare, TipPerStar);

        [Test]
        public void OnJobCompleted_AtTwoStars_NoTip()
        {
            var state = new RunState { Money = 0f };
            var economy = CreateSystem(state);
            var order = new OrderInstance(1);

            var payout = economy.OnJobCompleted(order, 2f);

            Assert.AreEqual(BaseFare, payout);
            Assert.AreEqual(BaseFare, state.Money);
            Assert.AreEqual(BaseFare, state.CumulativeProfit);
            Assert.AreEqual(BaseFare, state.PeakProfit);
            Assert.AreEqual(1, state.SuccessfulJobs);
        }

        [Test]
        public void OnJobCompleted_AtFourStars_EarnsTip()
        {
            var state = new RunState { Money = 0f };
            var economy = CreateSystem(state);
            var order = new OrderInstance(1);
            var expectedTip = TipPerStar * (4 - 3 + 1);

            var payout = economy.OnJobCompleted(order, 4f);

            Assert.Greater(expectedTip, 0f);
            Assert.AreEqual(BaseFare + expectedTip, payout);
            Assert.AreEqual(BaseFare + expectedTip, state.Money);
            Assert.AreEqual(BaseFare + expectedTip, state.PeakProfit);
        }

        [Test]
        public void OnJobCompleted_UpdatesPeakProfitAcrossJobs()
        {
            var state = new RunState { Money = 0f };
            var economy = CreateSystem(state);
            var order = new OrderInstance(1);

            economy.OnJobCompleted(order, 4f);
            var peakAfterFirst = state.PeakProfit;

            economy.ApplyDailyUpkeep(new List<HubDef>
            {
                CreateHubDef(50f)
            });

            Assert.Less(state.CumulativeProfit, peakAfterFirst);
            Assert.AreEqual(peakAfterFirst, state.PeakProfit);
        }

        [Test]
        public void ApplyDailyUpkeep_DeductsSumOfHubCosts()
        {
            var state = new RunState { Money = 100f, CumulativeProfit = 100f };
            var economy = CreateSystem(state);
            var hubs = new List<HubDef>
            {
                CreateHubDef(10f),
                CreateHubDef(15f)
            };

            var upkeep = economy.ApplyDailyUpkeep(hubs);

            Assert.AreEqual(25f, upkeep);
            Assert.AreEqual(75f, state.Money);
            Assert.AreEqual(75f, state.CumulativeProfit);
        }

        static HubDef CreateHubDef(float dailyUpkeep)
        {
            var hub = ScriptableObject.CreateInstance<HubDef>();
            hub.dailyUpkeep = dailyUpkeep;
            return hub;
        }
    }
}
