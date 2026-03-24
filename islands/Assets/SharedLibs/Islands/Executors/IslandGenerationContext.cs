using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    public class IslandGenerationContext : SerializedMonoBehaviour
    {
        [SerializeField, Required, InlineEditor]
        private IslandShapePreset shapePreset;

        [SerializeField, Required, InlineEditor]
        private IslandWaterPreset waterPreset;

        [SerializeField, InlineProperty, HideLabel]
        private IslandShapeRequest request = new IslandShapeRequest();

        private Vector2[][] cachedContours;
        private int cachedHash;

        public IslandShapePreset ShapePreset => shapePreset;
        public IslandShapeRequest Request => request;
        public IslandWaterPreset WaterPreset => waterPreset;
        public int ContourSegments => shapePreset != null ? shapePreset.ContourSegments : 256;

        public bool TryGetShapePreset(out IslandShapePreset resolvedShapePreset)
        {
            resolvedShapePreset = shapePreset;
            return resolvedShapePreset != null;
        }

        public bool TryGetRequest(out IslandShapeRequest resolvedRequest)
        {
            resolvedRequest = request;
            return resolvedRequest != null;
        }

        public bool TryGetWaterPreset(out IslandWaterPreset resolvedWaterPreset)
        {
            resolvedWaterPreset = waterPreset;
            return resolvedWaterPreset != null;
        }

        public bool TryGetContours(out Vector2[][] contours)
        {
            contours = null;
            if (!TryGetShapePreset(out var resolvedShapePreset) || !TryGetRequest(out var resolvedRequest))
            {
                return false;
            }

            EnsureContoursUpToDate(resolvedShapePreset, resolvedRequest);
            contours = cachedContours;
            return contours != null && contours.Length > 0;
        }

        public int GetStableHashCode()
        {
            unchecked
            {
                var hash = shapePreset != null ? shapePreset.GetStableHashCode() : 17;
                hash = hash * 31 + (request != null ? request.GetStableHashCode() : 0);
                hash = hash * 31 + (waterPreset != null ? waterPreset.GetStableHashCode() : 0);
                return hash;
            }
        }

        private void OnValidate()
        {
            InvalidateCache();
        }

        private void EnsureContoursUpToDate(IslandShapePreset resolvedShapePreset, IslandShapeRequest resolvedRequest)
        {
            var hash = GetStableHashCode();
            if (cachedContours != null && cachedHash == hash)
            {
                return;
            }

            cachedContours = new IslandContourGenerator(resolvedShapePreset, resolvedRequest, resolvedShapePreset.ContourSegments).Execute();
            cachedHash = hash;
        }

        private void InvalidateCache()
        {
            cachedContours = null;
            cachedHash = 0;
        }
    }
}