using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Service Module Def")]
    public class ServiceModuleDef : ScriptableObject
    {
        public ServiceModule module;
        public string displayName;
        public Color color = Color.white;
        public float unlockCost;
        public int starterVehicleCount = 1;
        public VehicleType starterVehicleType;
        public AnimationCurve demandWeightByDayFraction = AnimationCurve.Constant(0f, 1f, 1f);
        public float basePatienceSeconds = 120f;
        public float graceSeconds = 30f;
    }
}
