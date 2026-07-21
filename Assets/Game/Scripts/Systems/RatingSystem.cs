using TrafficSim.Core;
using TrafficSim.Data;
using UnityEngine;

namespace TrafficSim.Systems
{
    public sealed class RatingSystem
    {
        readonly RunState _state;
        readonly RatingDef _def;

        public RatingSystem(RunState state, RatingDef def)
        {
            _state = state;
            _def = def;
        }

        public void ApplyJobOutcome(float remainingPatienceFraction)
        {
            var delta = _def.GetRatingDelta(remainingPatienceFraction);
            _state.CurrentStars = Mathf.Clamp(_state.CurrentStars + delta, 1f, 5f);
        }

        public void SnapshotEndOfDay()
        {
            var roundedStars = Mathf.RoundToInt(_state.CurrentStars);
            if (roundedStars <= 1)
                _state.ConsecutiveOneStarDays++;
            else
                _state.ConsecutiveOneStarDays = 0;
        }

        public bool ShouldFailStreak(RatingDef def) =>
            _state.ConsecutiveOneStarDays >= def.streakFailDays;
    }
}
