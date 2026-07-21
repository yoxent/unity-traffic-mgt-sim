using System;
using UnityEngine;

namespace TrafficSim.Core
{
    public sealed class DayClock
    {
        public float DayLengthSeconds { get; }
        public float DayFraction { get; private set; }
        public int TimeScale { get; private set; } = 1;
        public bool IsPaused { get; set; }

        public event Action DayEnded;

        public DayClock(float dayLengthSeconds)
        {
            DayLengthSeconds = dayLengthSeconds;
        }

        public void SetTimeScale(int scale) => TimeScale = Mathf.Clamp(scale, 1, 3);

        public void Advance(float deltaTime)
        {
            if (IsPaused || DayLengthSeconds <= 0f)
                return;

            var previousFraction = DayFraction;
            DayFraction += deltaTime * TimeScale / DayLengthSeconds;

            if (previousFraction < 1f && DayFraction >= 1f)
            {
                DayFraction = 1f;
                DayEnded?.Invoke();
            }
        }

        public void ResetDay() => DayFraction = 0f;
    }
}
