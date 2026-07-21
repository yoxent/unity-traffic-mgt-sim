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

            if (_rating.ShouldFailStreak(_ratingDef))
            {
                _state.Phase = RunPhase.Failed;
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
            _economy?.ApplyDailyUpkeep(_activeHubs ?? Array.Empty<HubDef>());
            _queue.ApplyAll(_state);
        }

        public void AdvanceDay()
        {
            _state.DayIndex++;
            _clock.ResetDay();
            _state.Phase = RunPhase.Playing;
            _queue.Clear();
            DayAdvanced?.Invoke();
        }
    }
}
