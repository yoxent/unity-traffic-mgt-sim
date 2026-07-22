using System;
using UnityEngine;

namespace TrafficSim.Data
{
    [Serializable]
    public class HouseLot
    {
        /// <summary>
        /// Min cell of the footprint (x → world X, y → world Z).
        /// A 1×2 lot occupies (origin.x, origin.y) and (origin.x, origin.y + 1).
        /// A 2×1 lot occupies (origin.x, origin.y) and (origin.x + 1, origin.y).
        /// </summary>
        public Vector2Int origin;
        /// <summary>Exactly 1×2 or 2×1 cell footprint.</summary>
        public Vector2Int footprint = new(1, 2);
    }
}
