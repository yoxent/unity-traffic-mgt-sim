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

        public MapLoadResult(MapSkeleton skeleton, RoadGraph graph)
        {
            Skeleton = skeleton;
            Graph = graph;
        }
    }

    public static class MapLoader
    {
        public static RoadGraph Load(MapSkeleton skeleton) =>
            BuildGraph(skeleton);

        public static async Awaitable<MapLoadResult> LoadAsync(string address)
        {
            AsyncOperationHandle<MapSkeleton> handle = default;

            try
            {
                handle = Addressables.LoadAssetAsync<MapSkeleton>(address);
                var skeleton = await handle.Task;

                if (skeleton == null)
                {
                    Debug.LogError($"MapLoader: Addressable '{address}' returned null.");
                    return default;
                }

                return new MapLoadResult(skeleton, BuildGraph(skeleton));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"MapLoader: failed to load '{address}'. {ex.Message}");
                return default;
            }
        }

        public static void Release(MapSkeleton skeleton)
        {
            if (skeleton != null)
                Addressables.Release(skeleton);
        }

        static RoadGraph BuildGraph(MapSkeleton skeleton) =>
            RoadGraph.BuildLineGraph(skeleton.roadNodePositions);
    }
}
