using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    public class IslandGenerationContext : SerializedMonoBehaviour
    {
        [SerializeField, Required, AssetsOnly]
        private IslandWaterPreset waterPreset;

        [SerializeField, InlineProperty, HideLabel]
        private IslandShapeRequest request = new IslandShapeRequest();

        [SerializeField, Range(64, 1024)]
        private int contourSegments = 256;

        private Vector2[][] cachedContours;
        private int cachedHash;

        public IslandShapeRequest Request => request;
        public IslandWaterPreset WaterPreset => waterPreset;
        public int ContourSegments => contourSegments;

        public bool TryGetRequest(out IslandShapeRequest resolvedRequest)
        {
            resolvedRequest = request;
            return resolvedRequest != null && resolvedRequest.Preset != null;
        }

        public bool TryGetWaterPreset(out IslandWaterPreset resolvedWaterPreset)
        {
            resolvedWaterPreset = waterPreset;
            return resolvedWaterPreset != null;
        }

        public bool TryGetContours(out Vector2[][] contours)
        {
            contours = null;
            if (!TryGetRequest(out var resolvedRequest))
            {
                return false;
            }

            EnsureContoursUpToDate(resolvedRequest);
            contours = cachedContours;
            return contours != null && contours.Length > 0;
        }

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = request != null ? request.GetStableHashCode() : 17;
                hash = hash * 31 + (waterPreset != null ? waterPreset.GetStableHashCode() : 0);
                hash = hash * 31 + contourSegments;
                return hash;
            }
        }

        private void OnValidate()
        {
            InvalidateCache();
        }

        private void EnsureContoursUpToDate(IslandShapeRequest resolvedRequest)
        {
            var hash = GetStableHashCode();
            if (cachedContours != null && cachedHash == hash)
            {
                return;
            }

            cachedContours = new IslandContourGenerator(resolvedRequest, contourSegments).Execute();
            cachedHash = hash;
        }

        private void InvalidateCache()
        {
            cachedContours = null;
            cachedHash = 0;
        }
    }
}