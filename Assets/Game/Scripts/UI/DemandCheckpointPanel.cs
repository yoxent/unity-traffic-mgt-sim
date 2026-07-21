using System.Text;
using TrafficSim.Core;
using TrafficSim.Demand;
using UnityEngine;

namespace TrafficSim.UI
{
    public sealed class DemandCheckpointPanel : MonoBehaviour
    {
        [SerializeField] UiTextRef[] _checkpointLines = new UiTextRef[3];

        DemandSpawner _spawner;
        DayClock _clock;
        float _dayLengthSeconds;

        public void Bind(DemandSpawner spawner, DayClock clock, float dayLengthSeconds)
        {
            _spawner = spawner;
            _clock = clock;
            _dayLengthSeconds = dayLengthSeconds;
        }

        void LateUpdate()
        {
            if (_spawner == null || _clock == null)
                return;

            var checkpoints = _spawner.GetUpcomingCheckpoints(_checkpointLines.Length);
            for (var i = 0; i < _checkpointLines.Length; i++)
            {
                var line = _checkpointLines[i];
                if (line == null)
                    continue;

                if (i < checkpoints.Count)
                    line.SetText(FormatCheckpoint(checkpoints[i]));
                else
                    line.SetText(string.Empty);
            }
        }

        string FormatCheckpoint(DemandCheckpoint checkpoint)
        {
            var etaSeconds = (checkpoint.DayFraction - _clock.DayFraction) * _dayLengthSeconds;
            if (etaSeconds < 0f)
                etaSeconds = 0f;

            return $"{checkpoint.Module} {checkpoint.SizeBand} x{checkpoint.Count} — {FormatEta(etaSeconds)}";
        }

        static string FormatEta(float etaSeconds)
        {
            if (etaSeconds < 60f)
                return $"{Mathf.CeilToInt(etaSeconds)}s";

            return $"{Mathf.CeilToInt(etaSeconds / 60f)}m";
        }
    }
}
