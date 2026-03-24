using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [CreateAssetMenu(menuName = "Islands/Island Water Preset", fileName = "IslandWaterPreset")]
    public class IslandWaterPreset : SerializedScriptableObject
    {
        [SerializeField, BoxGroup("Rivers"), Range(0f, 1f)]
        private float riverAbundance = 0.5f;

        [SerializeField, BoxGroup("Rivers"), Range(6, 64)]
        private int riverPointCount = 18;

        [SerializeField, BoxGroup("Rivers"), Range(0.20f, 0.85f)]
        private float inlandReach = 0.60f;

        [SerializeField, BoxGroup("Rivers"), Range(0.08f, 0.45f)]
        private float minimumRiverLength = 0.24f;

        [SerializeField, BoxGroup("Rivers"), Range(0f, 0.15f)]
        private float meanderStrength = 0.02f;

        [SerializeField, BoxGroup("Rivers"), Range(0f, 1f)]
        private float smoothingStrength = 0.78f;

        [SerializeField, BoxGroup("Rivers"), Range(0f, 0.5f)]
        private float mouthJitter = 0.12f;

        [SerializeField, BoxGroup("Rivers"), Range(0.02f, 0.20f)]
        private float mouthSpacing = 0.06f;

        [SerializeField, BoxGroup("Branches"), Range(0f, 1f)]
        private float branchChance = 0.45f;

        [SerializeField, BoxGroup("Branches"), Range(0.15f, 0.85f)]
        private float branchingStart = 0.58f;

        public float RiverAbundance => riverAbundance;
        public int RiverPointCount => Mathf.Clamp(riverPointCount, 6, 64);
        public float InlandReach => inlandReach;
        public float MinimumRiverLength => minimumRiverLength;
        public float MeanderStrength => meanderStrength;
        public float SmoothingStrength => smoothingStrength;
        public float MouthJitter => mouthJitter;
        public float MouthSpacing => mouthSpacing;
        public float BranchChance => branchChance;
        public float BranchingStart => branchingStart;

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + riverAbundance.GetHashCode();
                hash = hash * 31 + RiverPointCount;
                hash = hash * 31 + inlandReach.GetHashCode();
                hash = hash * 31 + minimumRiverLength.GetHashCode();
                hash = hash * 31 + meanderStrength.GetHashCode();
                hash = hash * 31 + smoothingStrength.GetHashCode();
                hash = hash * 31 + mouthJitter.GetHashCode();
                hash = hash * 31 + mouthSpacing.GetHashCode();
                hash = hash * 31 + branchChance.GetHashCode();
                hash = hash * 31 + branchingStart.GetHashCode();
                return hash;
            }
        }
    }
}