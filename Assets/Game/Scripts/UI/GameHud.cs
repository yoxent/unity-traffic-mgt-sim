using TrafficSim.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrafficSim.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        /// <summary>In-game hour at dayFraction 0 (Morning starts at 06:00).</summary>
        public const float DayStartHour = 6f;

        [SerializeField] UiTextRef _moneyText;
        [SerializeField] UiTextRef _starsText;
        [SerializeField] UiTextRef _dayText;
        [SerializeField] UiTextRef _speedText;
        [SerializeField] UiTextRef _timeOfDayText;
        [SerializeField] UiTextRef _clockText;
        [SerializeField] Image _dayProgressFill;

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

            var fraction = Mathf.Clamp01(_clock.DayFraction);

            _moneyText?.SetText($"${Mathf.FloorToInt(_state.Money)}");
            _starsText?.SetText($"{_state.CurrentStars:F1} stars");
            _dayText?.SetText($"Day {_state.DayIndex + 1}");
            _speedText?.SetText(FormatSpeed());
            var period = FormatTimeOfDay(fraction);
            _clockText?.SetText($"{FormatClock(fraction)} · {period}");
            _timeOfDayText?.SetText(period);

            if (_dayProgressFill != null)
                _dayProgressFill.fillAmount = fraction;
        }

        string FormatSpeed()
        {
            if (_clock.IsPaused)
                return "Paused";

            return $"{_clock.TimeScale}x";
        }

        /// <summary>
        /// Maps dayFraction 0→1 to a 24h clock starting at <see cref="DayStartHour"/>.
        /// Bands: Morning 06–12, Day 12–18, Evening 18–00, Night 00–06.
        /// </summary>
        public static string FormatClock(float dayFraction)
        {
            var totalHours = DayStartHour + Mathf.Clamp01(dayFraction) * 24f;
            var hour24 = Mathf.FloorToInt(totalHours) % 24;
            var minute = Mathf.FloorToInt((totalHours - Mathf.Floor(totalHours)) * 60f);
            return $"{hour24:00}:{minute:00}";
        }

        public static string FormatTimeOfDay(float dayFraction)
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
