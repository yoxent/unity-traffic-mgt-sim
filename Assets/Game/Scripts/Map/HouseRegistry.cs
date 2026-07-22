using System;
using System.Collections.Generic;
using TrafficSim.Core.Linq;
using TrafficSim.Data;
using UnityEngine;

namespace TrafficSim.Map
{
    public sealed class HouseInstance
    {
        public int Id { get; }
        public Vector2Int Origin { get; }
        public Vector3 Center { get; }
        public Vector2Int Footprint { get; }
        public int DropoffNodeId { get; }

        public HouseInstance(int id, Vector2Int origin, Vector2Int footprint, int dropoffNodeId)
        {
            Id = id;
            Origin = origin;
            Center = MapGridSpec.FootprintCenter(origin, footprint);
            Footprint = footprint;
            DropoffNodeId = dropoffNodeId;
        }
    }

    public sealed class HouseRegistry
    {
        readonly HouseInstance[] _houses;

        public int Count => _houses.Length;

        public HouseRegistry(IReadOnlyList<HouseInstance> houses)
        {
            if (houses == null || houses.Count == 0)
            {
                _houses = Array.Empty<HouseInstance>();
                return;
            }

            _houses = new HouseInstance[houses.Count];
            for (var i = 0; i < houses.Count; i++)
                _houses[i] = houses[i];
        }

        public HouseInstance GetById(int houseId)
        {
            for (var i = 0; i < _houses.Length; i++)
            {
                if (_houses[i].Id == houseId)
                    return _houses[i];
            }

            return null;
        }

        public HouseInstance GetByIndex(int index) => _houses[index];

        public static HouseRegistry Build(MapSkeleton skeleton, RoadGraph graph)
        {
            var lots = skeleton?.houseLots;
            if (lots == null || lots.Length == 0 || graph == null || graph.NodeCount == 0)
                return new HouseRegistry(Array.Empty<HouseInstance>());

            var houses = new List<HouseInstance>(lots.Length);
            for (var i = 0; i < lots.Length; i++)
            {
                var lot = lots[i];
                if (lot == null || !MapGridSpec.IsValidHouseFootprint(lot.footprint))
                    continue;

                var center = MapGridSpec.FootprintCenter(lot.origin, lot.footprint);
                var dropoffNode = SimLinq.FindNearestNodeIndex(graph, center);
                houses.Add(new HouseInstance(i, lot.origin, lot.footprint, dropoffNode));
            }

            return new HouseRegistry(houses);
        }
    }
}
