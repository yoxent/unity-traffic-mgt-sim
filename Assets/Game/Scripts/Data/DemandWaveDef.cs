using System;
using System.Collections.Generic;
using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.Data
{
    [Serializable]
    public struct DemandWaveEntry
    {
        public float daySecond;
        public ServiceModule module;
        public JobSizeBand sizeBand;
        public int count;
    }

    [CreateAssetMenu(menuName = "TrafficSim/Data/Demand Wave Def")]
    public class DemandWaveDef : ScriptableObject
    {
        public List<DemandWaveEntry> waves = new();
    }
}
