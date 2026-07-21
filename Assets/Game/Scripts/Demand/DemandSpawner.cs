using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Demand
{
    public sealed class DemandSpawner
    {
        readonly DemandWaveDef _waveDef;
        readonly float _dayLengthSeconds;
        readonly IReadOnlyDictionary<ServiceModule, ServiceModuleDef> _moduleDefs;
        readonly RoadGraph _graph;
        readonly List<OrderInstance> _orders = new();
        readonly HashSet<int> _spawnedWaveIndices = new();
        int _nextOrderId = 1;
        float _dayFraction;

        public DemandSpawner(
            DemandWaveDef waveDef,
            float dayLengthSeconds,
            IReadOnlyDictionary<ServiceModule, ServiceModuleDef> moduleDefs,
            RoadGraph graph)
        {
            _waveDef = waveDef ?? throw new ArgumentNullException(nameof(waveDef));
            _dayLengthSeconds = dayLengthSeconds;
            _moduleDefs = moduleDefs ?? throw new ArgumentNullException(nameof(moduleDefs));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public IReadOnlyList<OrderInstance> Orders => _orders;

        public float DayFraction => _dayFraction;

        public void Tick(float dayFraction)
        {
            _dayFraction = dayFraction;

            for (var i = 0; i < _waveDef.waves.Count; i++)
            {
                if (_spawnedWaveIndices.Contains(i))
                    continue;

                var entry = _waveDef.waves[i];
                var threshold = entry.daySecond / _dayLengthSeconds;
                if (dayFraction < threshold)
                    continue;

                _spawnedWaveIndices.Add(i);
                SpawnWave(entry, dayFraction);
            }
        }

        public IReadOnlyList<DemandCheckpoint> GetUpcomingCheckpoints(int maxCount = 3)
        {
            var upcoming = new List<(float dayFraction, DemandWaveEntry entry)>();

            for (var i = 0; i < _waveDef.waves.Count; i++)
            {
                if (_spawnedWaveIndices.Contains(i))
                    continue;

                var entry = _waveDef.waves[i];
                var dayFraction = entry.daySecond / _dayLengthSeconds;
                if (dayFraction <= _dayFraction)
                    continue;

                upcoming.Add((dayFraction, entry));
            }

            upcoming.Sort((a, b) => a.dayFraction.CompareTo(b.dayFraction));

            var count = Mathf.Min(maxCount, upcoming.Count);
            var checkpoints = new DemandCheckpoint[count];
            for (var i = 0; i < count; i++)
            {
                var (dayFraction, entry) = upcoming[i];
                checkpoints[i] = new DemandCheckpoint(
                    dayFraction,
                    entry.module,
                    entry.sizeBand,
                    GetSpawnCount(entry, dayFraction));
            }

            return checkpoints;
        }

        void SpawnWave(DemandWaveEntry entry, float dayFraction)
        {
            var spawnCount = GetSpawnCount(entry, dayFraction);
            if (spawnCount <= 0 || _graph.NodeCount == 0)
                return;

            if (!_moduleDefs.TryGetValue(entry.module, out var moduleDef))
                return;

            for (var i = 0; i < spawnCount; i++)
            {
                var orderId = _nextOrderId++;
                var pickupNode = orderId % _graph.NodeCount;
                var dropoffNode = (pickupNode + 1) % _graph.NodeCount;

                _orders.Add(new OrderInstance(
                    orderId,
                    entry.module,
                    entry.sizeBand,
                    pickupNode,
                    dropoffNode,
                    moduleDef.basePatienceSeconds,
                    moduleDef.graceSeconds));
            }
        }

        int GetSpawnCount(DemandWaveEntry entry, float dayFraction)
        {
            var multiplier = 1f;
            if (_moduleDefs.TryGetValue(entry.module, out var moduleDef) &&
                moduleDef.demandWeightByDayFraction != null)
            {
                multiplier = moduleDef.demandWeightByDayFraction.Evaluate(dayFraction);
            }

            return Mathf.Max(0, Mathf.RoundToInt(entry.count * multiplier));
        }
    }
}
