using System;
using UnityEngine;

namespace TrafficSim.Data
{
    [Serializable]
    public class RoadBranch
    {
        /// <summary>Polyline from junction through branch terminus. First point should match a main-road node.</summary>
        public Vector3[] nodePositions;
    }
}
