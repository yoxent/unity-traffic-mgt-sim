using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Vehicle Def")]
    public class VehicleDef : ScriptableObject
    {
        public VehicleType type;
        public Color color = Color.white;
        public float speed = 5f;
        public float maxRange = 100f;
        public float maxDurability = 100f;
        public float durabilityLossPerJob = 5f;
        public float repairCost = 10f;
        public float purchaseCost = 100f;
        public float cooldownSeconds = 2f;
        public ServiceModule[] allowedModules;
        public JobSizeBand[] allowedSizeBands;

        public bool CanServe(ServiceModule module, JobSizeBand band)
        {
            if (allowedModules == null || allowedSizeBands == null)
                return false;

            var moduleAllowed = false;
            for (var i = 0; i < allowedModules.Length; i++)
            {
                if (allowedModules[i] == module)
                {
                    moduleAllowed = true;
                    break;
                }
            }

            if (!moduleAllowed)
                return false;

            for (var i = 0; i < allowedSizeBands.Length; i++)
            {
                if (allowedSizeBands[i] == band)
                    return true;
            }

            return false;
        }
    }
}
