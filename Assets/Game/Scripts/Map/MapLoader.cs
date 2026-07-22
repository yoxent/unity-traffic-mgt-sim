using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TrafficSim.Map
{
    public readonly struct MapLoadResult
    {
        public MapSkeleton Skeleton { get; }
        public RoadGraph Graph { get; }
        public HouseRegistry Houses { get; }

        public MapLoadResult(MapSkeleton skeleton, RoadGraph graph, HouseRegistry houses)
        {
            Skeleton = skeleton;
            Graph = graph;
            Houses = houses;
        }
    }

    public static class MapLoader
    {
        public static MapLoadResult Load(MapSkeleton skeleton) =>
            BuildResult(skeleton);

        public static async Awaitable<MapLoadResult> LoadAsync(string address)
        {
            AsyncOperationHandle<MapSkeleton> handle = default;

            try
            {
                handle = Addressables.LoadAssetAsync<MapSkeleton>(address);
                var skeleton = await handle.Task;

                if (skeleton == null)
                {
                    SimLog.Error("Map", $"Addressable '{address}' returned null.");
                    return default;
                }

                SimLog.MapInfo($"Addressables loaded '{address}' → '{skeleton.name}'");
                return BuildResult(skeleton);
            }
            catch (System.Exception ex)
            {
                SimLog.Error("Map", $"Failed to load '{address}'. {ex.Message}");
                return default;
            }
        }

        public static void Release(MapSkeleton skeleton)
        {
            if (skeleton != null)
            {
                SimLog.MapInfo($"Addressables release '{skeleton.name}'");
                Addressables.Release(skeleton);
            }
        }

        static MapLoadResult BuildResult(MapSkeleton skeleton)
        {
            var graph = BuildGraph(skeleton);
            var houses = HouseRegistry.Build(skeleton, graph);
            SimLog.MapInfo(
                $"Map '{skeleton.name}' houses={houses.Count} nodes={graph.NodeCount} hubSlots={skeleton.hubSlotPositions?.Length ?? 0}");
            return new MapLoadResult(skeleton, graph, houses);
        }

        static RoadGraph BuildGraph(MapSkeleton skeleton)
        {
            var polylines = new List<IReadOnlyList<Vector3>>();
            if (skeleton.roadNodePositions is { Length: > 0 })
                polylines.Add(skeleton.roadNodePositions);

            if (skeleton.roadBranches != null)
            {
                for (var i = 0; i < skeleton.roadBranches.Length; i++)
                {
                    var branch = skeleton.roadBranches[i];
                    if (branch?.nodePositions is { Length: > 0 })
                        polylines.Add(branch.nodePositions);
                }
            }

            var graph = RoadGraph.BuildFromPolylines(polylines);
            SimLog.MapInfo(
                $"Built road graph from '{skeleton.name}' nodes={graph.NodeCount} branches={skeleton.roadBranches?.Length ?? 0} hubSlots={skeleton.hubSlotPositions?.Length ?? 0}");
            return graph;
        }
    }
}
