using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [CreateAssetMenu(menuName = "Islands/Island Water Preset", fileName = "IslandWaterPreset")]
    public class IslandWaterPreset : SerializedScriptableObject
    {
        [SerializeField, BoxGroup("Sources"), Range(0, 20)]
        private int flowSources = 2;

        [SerializeField, BoxGroup("Branches"), Range(0f, 0.35f)]
        private float branchProbability = 0.08f;

        [SerializeField, BoxGroup("Detail"), Range(6, 64)]
        private int riverPointCount = 18;

        [SerializeField, BoxGroup("Meanders"), Range(0.1f, 100f)]
        private float meanderLength = 4f;

        [SerializeField, BoxGroup("Meanders"), Range(0f, 100f)]
        private float meanderWidth = 2f;

        public int FlowSources => flowSources;
        public float BranchProbability => branchProbability;
        public int RiverPointCount => riverPointCount;
        public float MeanderLength => meanderLength;
        public float MeanderWidth => meanderWidth;

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + FlowSources;
                hash = hash * 31 + BranchProbability.GetHashCode();
                hash = hash * 31 + RiverPointCount;
                hash = hash * 31 + MeanderLength.GetHashCode();
                hash = hash * 31 + MeanderWidth.GetHashCode();
                return hash;
            }
        }
    }
}