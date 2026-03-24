using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [Serializable]
    public class IslandShapeRequest
    {
        [Range(30,100)]
        public float TargetArea = 30f;

        [Range(0.1f, 20f)]
        public float TargetMaxElevation = 2.4f;

        [Range(50f, 200f)]
        public float AspectPercent = 120f;

        [Range(25f, 200f)]
        public float ReliefComplexityPercent = 130f;

        [Range(0,20)]
        public float CoastlineComplexity = 1f;

        [Range(0,20)]
        public int OffshoreIsletCount = 1;

        [Range(-180f, 180f)]
        public float Direction = 35f;

        public int Seed = 12345;

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + TargetArea.GetHashCode();
                hash = hash * 31 + TargetMaxElevation.GetHashCode();
                hash = hash * 31 + AspectPercent.GetHashCode();
                hash = hash * 31 + ReliefComplexityPercent.GetHashCode();
                hash = hash * 31 + CoastlineComplexity.GetHashCode();
                hash = hash * 31 + OffshoreIsletCount;
                hash = hash * 31 + Direction.GetHashCode();
                hash = hash * 31 + Seed;
                return hash;
            }
        }
    }
}