using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Map Skeleton")]
    public class MapSkeleton : ScriptableObject
    {
        public string[] districtUnlockIds;
        /// <summary>Cell-center world positions (integer X/Z) for hub placement.</summary>
        public Vector3[] hubSlotPositions;
        /// <summary>Cell-center world positions (integer X/Z) for road graph nodes.</summary>
        public Vector3[] roadNodePositions;
        /// <summary>Optional branch polylines (T-junction spurs, side streets).</summary>
        public RoadBranch[] roadBranches;
        /// <summary>District cell range; X/Y are grid axes mapped to world X/Z.</summary>
        public BoundsInt blockedGridBounds;
        /// <summary>Blocked cell centers (x → world X, y → world Z). Water, parks, etc.</summary>
        public Vector2Int[] blockedCells;
        /// <summary>Destination houses (1×2 or 2×1 footprints).</summary>
        public HouseLot[] houseLots;
    }
}
