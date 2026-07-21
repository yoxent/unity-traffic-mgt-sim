using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] UiTextRef _moneyText;
        [SerializeField] UiTextRef _starsText;
        [SerializeField] UiTextRef _dayText;
        [SerializeField] UiTextRef _speedText;
        [SerializeField] UiTextRef _timeOfDayText;

        RunState _state;
        DayClock _clock;

        public void Bind(RunState state, DayClock clock)
        {
            _state = state;
            _clock = clock;
        }

        void LateUpdate()
        {
            if (_state == null || _clock == null)
                return;

            _moneyText?.SetText($"${Mathf.FloorToInt(_state.Money)}");
            _starsText?.SetText($"{_state.CurrentStars:F1}★");
            _dayText?.SetText($"Day {_state.DayIndex + 1}");
            _speedText?.SetText(FormatSpeed());
            _timeOfDayText?.SetText(FormatTimeOfDay(_clock.DayFraction));
        }

        string FormatSpeed()
        {
            if (_clock.IsPaused)
                return "Paused";

            return $"{_clock.TimeScale}x";
        }

        static string FormatTimeOfDay(float dayFraction)
        {
            if (dayFraction < 0.25f)
                return "Morning";
            if (dayFraction < 0.5f)
                return "Day";
            if (dayFraction < 0.75f)
                return "Evening";

            return "Night";
        }
    }
}
