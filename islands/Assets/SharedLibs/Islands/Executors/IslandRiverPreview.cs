using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [ExecuteAlways]
    public class IslandRiverPreview : SerializedMonoBehaviour
    {
        [SerializeField, Required]
        private IslandGenerationContext generationContext;

        [SerializeField, Range(6, 64)]
        private int riverPointCount = 18;

        [SerializeField]
        private bool regenerateAutomatically = true;

        [SerializeField]
        private bool drawWhenNotSelected;

        [SerializeField]
        private float gizmoHeight = 0f;

        [SerializeField]
        private Color riverColor = new Color(0.18f, 0.60f, 1.00f, 1f);

        private Vector4[][] cachedRivers;
        private int cachedHash;

        public void Regenerate()
        {
            if (!TryResolveInputs(out var request, out var waterPreset, out var contours))
            {
                cachedRivers = null;
                cachedHash = 0;
                return;
            }

            cachedRivers = new IslandRiverGenerator(request, contours, waterPreset, riverPointCount).Execute();
            cachedHash = ComputeHash();
        }

        private void OnEnable()
        {
            AutoBindContext();
            if (regenerateAutomatically)
            {
                Regenerate();
            }
        }

        private void OnValidate()
        {
            AutoBindContext();
            if (regenerateAutomatically)
            {
                Regenerate();
            }
        }

        private void OnDrawGizmos()
        {
            if (drawWhenNotSelected)
            {
                DrawPreview();
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawPreview();
        }

        private void DrawPreview()
        {
            EnsureUpToDate();
            if (cachedRivers == null)
            {
                return;
            }

            for (var i = 0; i < cachedRivers.Length; i++)
            {
                DrawRiver(cachedRivers[i]);
            }
        }

        private void EnsureUpToDate()
        {
            var hash = ComputeHash();
            if (cachedRivers == null || cachedHash != hash)
            {
                Regenerate();
            }
        }

        private int ComputeHash()
        {
            unchecked
            {
                var hash = generationContext != null ? generationContext.GetStableHashCode() : 17;
                hash = hash * 31 + riverPointCount;
                return hash;
            }
        }

        private bool TryResolveInputs(out IslandShapeRequest request, out IslandWaterPreset waterPreset, out Vector2[][] contours)
        {
            AutoBindContext();
            request = null;
            waterPreset = null;
            contours = null;
            if (generationContext == null)
            {
                Debug.LogWarning("Island river preview requires IslandGenerationContext.", this);
                return false;
            }

            if (!generationContext.TryGetRequest(out request) || !generationContext.TryGetWaterPreset(out waterPreset) || !generationContext.TryGetContours(out contours))
            {
                Debug.LogWarning("Island river preview requires a valid contour and water preset from IslandGenerationContext.", this);
                return false;
            }

            return true;
        }

        private void AutoBindContext()
        {
            if (generationContext == null)
            {
                generationContext = GetComponent<IslandGenerationContext>();
            }
        }

        private void DrawRiver(Vector4[] river)
        {
            if (river == null || river.Length < 2)
            {
                return;
            }

            Gizmos.color = riverColor;
            for (var i = 0; i < river.Length - 1; i++)
            {
                Gizmos.DrawLine(ToWorld(river[i]), ToWorld(river[i + 1]));
            }
        }

        private Vector3 ToWorld(Vector4 point)
        {
            return transform.TransformPoint(new Vector3(point.x, gizmoHeight + point.y, point.z));
        }
    }
}