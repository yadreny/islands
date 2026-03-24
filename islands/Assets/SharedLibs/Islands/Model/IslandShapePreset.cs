using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [CreateAssetMenu(menuName = "Islands/Island Shape Preset", fileName = "IslandShapePreset")]
    public class IslandShapePreset : SerializedScriptableObject
    {
        [SerializeField, BoxGroup("Reference"), Range(1f, 10000f)]
        private float recommendedArea = 300f;

        [SerializeField, BoxGroup("Reference"), Range(0.1f, 10000f)]
        private float recommendedMaxElevation = 900f;

        [SerializeField, BoxGroup("Reference"), Range(1f, 10f)]
        private float recommendedAspectRatio = 1.25f;

        [SerializeField, BoxGroup("Reference"), Range(0.01f, 5f)]
        private float recommendedReliefComplexity = 0.45f;

        [SerializeField, BoxGroup("Reference"), Range(64, 1024)]
        private int contourSegments = 256;

        [SerializeField, BoxGroup("Mass"), Range(0.2f, 1f)]
        private float footprintFill = 0.78f;

        [SerializeField, BoxGroup("Mass")]
        private IslandMassLayoutType massLayoutType = IslandMassLayoutType.SingleCore;

        [SerializeField, BoxGroup("Mass")]
        private IslandReliefProfile reliefProfile = IslandReliefProfile.BrokenMassif;

        [SerializeField, BoxGroup("Macro"), Range(0, 20)]
        private int lobeCount = 2;

        [SerializeField, BoxGroup("Macro"), Range(0, 20)]
        private int bayCount = 2;

        [SerializeField, BoxGroup("Macro"), Range(0f, 0.5f)]
        private float lobeStrength = 0.15f;

        [SerializeField, BoxGroup("Macro"), Range(0f, 0.5f)]
        private float bayDepth = 0.12f;

        [SerializeField, BoxGroup("Macro"), Range(0f, 1f)]
        private float peninsulaChance = 0.35f;

        [SerializeField, BoxGroup("Macro"), Range(0f, 0.5f)]
        private float peninsulaStrength = 0.18f;

        [SerializeField, BoxGroup("Coast"), Range(0f, 0.5f)]
        private float directionalBias = 0.14f;

        [SerializeField, BoxGroup("Coast"), Range(0f, 1f)]
        private float arcCurvature;

        [SerializeField, BoxGroup("Coast"), Range(0, 20)]
        private int ringBreakCount;

        public float RecommendedArea => recommendedArea;
        public float RecommendedMaxElevation => recommendedMaxElevation;
        public float RecommendedAspectRatio => recommendedAspectRatio;
        public float RecommendedReliefComplexity => recommendedReliefComplexity;
        public int ContourSegments => contourSegments;
        public float FootprintFill => footprintFill;
        public IslandMassLayoutType MassLayoutType => massLayoutType;
        public IslandReliefProfile ReliefProfile => reliefProfile;
        public int LobeCount => lobeCount;
        public int BayCount => bayCount;
        public float LobeStrength => lobeStrength;
        public float BayDepth => bayDepth;
        public float PeninsulaChance => peninsulaChance;
        public float PeninsulaStrength => peninsulaStrength;
        public float DirectionalBias => directionalBias;
        public float ArcCurvature => arcCurvature;
        public int RingBreakCount => ringBreakCount;

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + recommendedArea.GetHashCode();
                hash = hash * 31 + recommendedMaxElevation.GetHashCode();
                hash = hash * 31 + recommendedAspectRatio.GetHashCode();
                hash = hash * 31 + recommendedReliefComplexity.GetHashCode();
                hash = hash * 31 + ContourSegments;
                hash = hash * 31 + footprintFill.GetHashCode();
                hash = hash * 31 + (int)massLayoutType;
                hash = hash * 31 + (int)reliefProfile;
                hash = hash * 31 + lobeCount;
                hash = hash * 31 + bayCount;
                hash = hash * 31 + lobeStrength.GetHashCode();
                hash = hash * 31 + bayDepth.GetHashCode();
                hash = hash * 31 + peninsulaChance.GetHashCode();
                hash = hash * 31 + peninsulaStrength.GetHashCode();
                hash = hash * 31 + directionalBias.GetHashCode();
                hash = hash * 31 + arcCurvature.GetHashCode();
                hash = hash * 31 + ringBreakCount;
                return hash;
            }
        }
    }
}