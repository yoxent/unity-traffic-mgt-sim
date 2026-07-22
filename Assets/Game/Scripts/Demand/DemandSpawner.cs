using System;
using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Core.Contracts;
using TrafficSim.Data;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Demand
{
    public sealed class DemandSpawner : IDemandSource
    {
        readonly DemandWaveDef _waveDef;
        readonly float _dayLengthSeconds;
        readonly IReadOnlyDictionary<ServiceModule, ServiceModuleDef> _moduleDefs;
        readonly RoadGraph _graph;
        readonly HouseRegistry _houses;
        readonly List<OrderInstance> _orders = new();
        readonly HashSet<int> _spawnedWaveIndices = new();
        int _nextOrderId = 1;
        float _dayFraction;

        public DemandSpawner(
            DemandWaveDef waveDef,
            float dayLengthSeconds,
            IReadOnlyDictionary<ServiceModule, ServiceModuleDef> moduleDefs,
            RoadGraph graph,
            HouseRegistry houses)
        {
            _waveDef = waveDef ?? throw new ArgumentNullException(nameof(waveDef));
            _dayLengthSeconds = dayLengthSeconds;
            _moduleDefs = moduleDefs ?? throw new ArgumentNullException(nameof(moduleDefs));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _houses = houses ?? new HouseRegistry(Array.Empty<HouseInstance>());
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
                var (dropoffNode, destinationHouseId) = ResolveDestination(orderId);

                _orders.Add(new OrderInstance(
                    orderId,
                    entry.module,
                    entry.sizeBand,
                    pickupNode: -1,
                    dropoffNode,
                    moduleDef.basePatienceSeconds,
                    moduleDef.graceSeconds,
                    destinationHouseId));
            }

            SimLog.DemandInfo(
                $"Wave spawned module={entry.module} band={entry.sizeBand} count={spawnCount} " +
                $"dayFrac={dayFraction:F3} (~{entry.daySecond}s) totalOrders={_orders.Count}");
        }

        (int dropoffNode, int destinationHouseId) ResolveDestination(int orderId)
        {
            if (_houses.Count > 0)
            {
                var house = _houses.GetByIndex(orderId % _houses.Count);
                return (house.DropoffNodeId, house.Id);
            }

            if (_graph.NodeCount == 0)
                return (-1, -1);

            var fallbackNode = orderId % _graph.NodeCount;
            return (fallbackNode, -1);
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
