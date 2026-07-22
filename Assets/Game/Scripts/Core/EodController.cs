using System;
using System.Collections.Generic;
using TrafficSim.Data;
using TrafficSim.Systems;

namespace TrafficSim.Core
{
    public sealed class EodController
    {
        readonly RunState _state;
        readonly RatingSystem _rating;
        readonly RatingDef _ratingDef;
        readonly DayClock _clock;
        readonly EodActionQueue _queue;
        readonly EconomySystem _economy;
        readonly IReadOnlyList<HubDef> _activeHubs;

        public EodActionQueue Queue => _queue;

        public event Action EodStarted;
        public event Action DayAdvanced;

        public EodController(
            RunState state,
            RatingSystem rating,
            RatingDef ratingDef,
            DayClock clock,
            EodActionQueue queue = null,
            EconomySystem economy = null,
            IReadOnlyList<HubDef> activeHubs = null)
        {
            _state = state;
            _rating = rating;
            _ratingDef = ratingDef;
            _clock = clock;
            _queue = queue ?? new EodActionQueue();
            _economy = economy;
            _activeHubs = activeHubs;
        }

        public void BeginEod(bool skipIntervention = false)
        {
            _rating.SnapshotEndOfDay();
            SimLog.EodInfo(
                $"BeginEod day={_state.DayIndex} stars={_state.CurrentStars:F1} " +
                $"oneStarStreak={_state.ConsecutiveOneStarDays} money={_state.Money:F0} skip={skipIntervention}");

            if (_rating.ShouldFailStreak(_ratingDef))
            {
                _state.Phase = RunPhase.Failed;
                SimLog.PhaseInfo(
                    $"1★ streak fail after {_state.ConsecutiveOneStarDays} consecutive days (threshold={_ratingDef.streakFailDays})");
                EodStarted?.Invoke();
                return;
            }

            ApplyQueue();

            if (skipIntervention)
                AdvanceDay();
            else
            {
                _state.Phase = RunPhase.EodIntervention;
                EodStarted?.Invoke();
            }
        }

        public void ApplyQueue()
        {
            var moneyBefore = _state.Money;
            _economy?.ApplyDailyUpkeep(_activeHubs ?? Array.Empty<HubDef>());
            _queue.ApplyAll(_state);
            SimLog.EodInfo($"Applied EOD queue/upkeep money {moneyBefore:F0}→{_state.Money:F0}");
        }

        public void AdvanceDay()
        {
            _state.DayIndex++;
            _clock.ResetDay();
            _state.Phase = RunPhase.Playing;
            _queue.Clear();
            SimLog.EodInfo($"Advanced to day {_state.DayIndex + 1} (index={_state.DayIndex})");
            DayAdvanced?.Invoke();
        }
    }
}
