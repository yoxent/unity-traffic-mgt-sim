using System;
using TrafficSim.Events;
using TrafficSim.UI;

namespace TrafficSim.Core
{
    public sealed class SimEventBridge : IDisposable
    {
        readonly DayClock _clock;
        readonly EodController _eod;
        readonly GameEventChannel _dayEndedChannel;
        readonly GameEventChannel _eodStartedChannel;
        readonly GameEventChannel _dayAdvancedChannel;

        public SimEventBridge(
            DayClock clock,
            EodController eod,
            GameEventChannel dayEndedChannel,
            GameEventChannel eodStartedChannel,
            GameEventChannel dayAdvancedChannel)
        {
            _clock = clock;
            _eod = eod;
            _dayEndedChannel = dayEndedChannel;
            _eodStartedChannel = eodStartedChannel;
            _dayAdvancedChannel = dayAdvancedChannel;

            _clock.DayEnded += OnDayEnded;
            _eod.EodStarted += OnEodStarted;
            _eod.DayAdvanced += OnDayAdvanced;
        }

        public void Dispose()
        {
            _clock.DayEnded -= OnDayEnded;
            _eod.EodStarted -= OnEodStarted;
            _eod.DayAdvanced -= OnDayAdvanced;
        }

        void OnDayEnded()
        {
            _dayEndedChannel?.Raise();

            var skipIntervention = TutorialSaveStub.ShouldSkipEodUi;
            _eod.BeginEod(skipIntervention);
        }

        void OnEodStarted() => _eodStartedChannel?.Raise();

        void OnDayAdvanced() => _dayAdvancedChannel?.Raise();
    }
}
