using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Overload Def")]
    public class OverloadDef : ScriptableObject
    {
        public float capacityMultiplier = 1f;
    }
}
