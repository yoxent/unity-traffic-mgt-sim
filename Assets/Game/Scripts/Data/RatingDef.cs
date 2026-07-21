using UnityEngine;

namespace TrafficSim.Data
{
    [CreateAssetMenu(menuName = "TrafficSim/Data/Rating Def")]
    public class RatingDef : ScriptableObject
    {
        public int streakFailDays = 3;
        public float bandThreshold1 = 0.2f;
        public float bandThreshold2 = 0.4f;
        public float bandThreshold3 = 0.6f;
        public float bandThreshold4 = 0.8f;
        public float expiredDelta = -0.5f;
        public float band0Delta = -0.25f;
        public float band1Delta = -0.1f;
        public float band2Delta = 0.02f;
        public float band3Delta = 0.1f;
        public float band4Delta = 0.2f;
        public int minStarsForTips = 3;

        public float GetRatingDelta(float remainingFraction)
        {
            if (remainingFraction <= 0f)
                return expiredDelta;
            if (remainingFraction < bandThreshold1)
                return band0Delta;
            if (remainingFraction < bandThreshold2)
                return band1Delta;
            if (remainingFraction < bandThreshold3)
                return band2Delta;
            if (remainingFraction < bandThreshold4)
                return band3Delta;
            return band4Delta;
        }
    }
}
