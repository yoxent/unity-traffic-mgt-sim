using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Map Skeleton")]
    public class MapSkeleton : ScriptableObject
    {
        public string[] districtUnlockIds;
        public Vector3[] hubSlotPositions;
        public Vector3[] roadNodePositions;
        public BoundsInt blockedGridBounds;
    }
}
