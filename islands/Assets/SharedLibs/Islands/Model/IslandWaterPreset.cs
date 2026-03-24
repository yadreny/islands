using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [CreateAssetMenu(menuName = "Islands/Island Water Preset", fileName = "IslandWaterPreset")]
    public class IslandWaterPreset : SerializedScriptableObject
    {
        [SerializeField, BoxGroup("Sources"), Range(0, 8)]
        private int flowSources = 2;

        [SerializeField, BoxGroup("Branches"), Range(0f, 0.35f)]
        private float branchProbability = 0.08f;

        [SerializeField, BoxGroup("Detail"), Range(6, 64)]
        private int riverPointCount = 18;

        [SerializeField, BoxGroup("Meanders"), MinValue(0.1f)]
        private float meanderLength = 4f;

        [SerializeField, BoxGroup("Meanders"), MinValue(0f)]
        private float meanderWidth = 2f;

        public int FlowSources => Mathf.Clamp(flowSources, 0, 8);
        public float BranchProbability => Mathf.Clamp(branchProbability, 0f, 0.35f);
        public int RiverPointCount => Mathf.Clamp(riverPointCount, 6, 64);
        public float MeanderLength => Mathf.Max(0.1f, meanderLength);
        public float MeanderWidth => Mathf.Max(0f, meanderWidth);

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