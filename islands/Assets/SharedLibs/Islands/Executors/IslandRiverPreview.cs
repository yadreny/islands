using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [ExecuteAlways]
    public class IslandRiverPreview : SerializedMonoBehaviour
    {
        [SerializeField, Required]
        private IslandGenerationContext generationContext;

        [SerializeField]
        private Color riverColor = new Color(0.18f, 0.60f, 1.00f, 1f);

        [SerializeField]
        private Color sourceColor = new Color(0.25f, 0.95f, 1.00f, 1f);

        [SerializeField, Range(0.01f, 5f)]
        private float sourceMarkerRadius = 0.2f;

        private Vector4[][] cachedRivers;
        private int cachedHash;

        private void OnEnable()
        {
            AutoBindContext();
            Regenerate();
        }

        private void OnValidate()
        {
            AutoBindContext();
            Regenerate();
        }

        private void OnDrawGizmos()
        {
            DrawPreview();
        }

        private void OnDrawGizmosSelected()
        {
            DrawPreview();
        }

        private void Regenerate()
        {
            if (!TryResolveInputs(out var shapePreset, out var request, out var waterPreset, out var contours))
            {
                cachedRivers = null;
                cachedHash = 0;
                return;
            }

            cachedRivers = new IslandRiverGenerator(shapePreset, request, contours, waterPreset).Execute();
            cachedHash = ComputeHash();
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

            DrawSources();
        }

        private void DrawSources()
        {
            if (cachedRivers == null || cachedRivers.Length == 0)
            {
                return;
            }

            var sourcePoints = CollectSourcePoints();
            Gizmos.color = sourceColor;
            for (var i = 0; i < sourcePoints.Count; i++)
            {
                Gizmos.DrawSphere(ToWorld(sourcePoints[i]), sourceMarkerRadius);
            }
        }

        private List<Vector4> CollectSourcePoints()
        {
            var result = new List<Vector4>();
            var epsilon = Mathf.Max(0.02f, sourceMarkerRadius * 0.75f);

            for (var riverIndex = 0; riverIndex < cachedRivers.Length; riverIndex++)
            {
                var river = cachedRivers[riverIndex];
                if (river == null || river.Length == 0)
                {
                    continue;
                }

                var candidate = river[0];
                var candidatePoint = ToPlanar(candidate);
                var isDuplicate = false;
                for (var i = 0; i < result.Count; i++)
                {
                    if (Vector2.Distance(candidatePoint, ToPlanar(result[i])) <= epsilon)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(candidate);
                }
            }

            return result;
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
            return generationContext != null ? generationContext.GetStableHashCode() : 17;
        }

        private bool TryResolveInputs(out IslandShapePreset shapePreset, out IslandShapeRequest request, out IslandWaterPreset waterPreset, out Vector2[][] contours)
        {
            AutoBindContext();
            shapePreset = null;
            request = null;
            waterPreset = null;
            contours = null;
            if (generationContext == null)
            {
                Debug.LogWarning("Island river preview requires IslandGenerationContext.", this);
                return false;
            }

            if (!generationContext.TryGetShapePreset(out shapePreset) || !generationContext.TryGetRequest(out request) || !generationContext.TryGetWaterPreset(out waterPreset) || !generationContext.TryGetContours(out contours))
            {
                Debug.LogWarning("Island river preview requires a valid shape preset, contour and water preset from IslandGenerationContext.", this);
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

        private Vector2 ToPlanar(Vector4 point)
        {
            return new Vector2(point.x, point.z);
        }

        private Vector3 ToWorld(Vector4 point)
        {
            return transform.TransformPoint(new Vector3(point.x, point.y, point.z));
        }
    }
}