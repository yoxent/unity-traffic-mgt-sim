using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Hub Def")]
    public class HubDef : ScriptableObject
    {
        public ServiceModule module;
        public Vector2Int footprint = new(3, 3);
        public int capacity = 4;
        public float dailyUpkeep = 10f;
        public float relocateCost = 50f;
    }
}
