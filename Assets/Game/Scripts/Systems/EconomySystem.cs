using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using UnityEngine;

namespace TrafficSim.Systems
{
    public sealed class EconomySystem
    {
        readonly RunState _state;
        readonly RatingDef _ratingDef;
        readonly float _baseFare;
        readonly float _tipPerStar;

        public EconomySystem(
            RunState state,
            RatingDef ratingDef,
            float baseFare = 10f,
            float tipPerStar = 5f)
        {
            _state = state;
            _ratingDef = ratingDef;
            _baseFare = baseFare;
            _tipPerStar = tipPerStar;
        }

        public float OnJobCompleted(OrderInstance order, float currentStars)
        {
            var fare = GetFare(order);
            var tip = GetTip(currentStars);
            var total = fare + tip;

            _state.Money += total;
            _state.CumulativeProfit += total;
            if (_state.CumulativeProfit > _state.PeakProfit)
                _state.PeakProfit = _state.CumulativeProfit;
            _state.SuccessfulJobs++;

            return total;
        }

        public float ApplyDailyUpkeep(IReadOnlyList<HubDef> hubs)
        {
            if (hubs == null || hubs.Count == 0)
                return 0f;

            var total = 0f;
            for (var i = 0; i < hubs.Count; i++)
            {
                if (hubs[i] != null)
                    total += hubs[i].dailyUpkeep;
            }

            _state.Money -= total;
            _state.CumulativeProfit -= total;
            return total;
        }

        float GetFare(OrderInstance order) => _baseFare;

        float GetTip(float currentStars)
        {
            var roundedStars = Mathf.RoundToInt(currentStars);
            if (roundedStars < _ratingDef.minStarsForTips)
                return 0f;

            return _tipPerStar * (roundedStars - _ratingDef.minStarsForTips + 1);
        }
    }
}
