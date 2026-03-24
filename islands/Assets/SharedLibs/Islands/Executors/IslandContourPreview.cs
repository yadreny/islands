using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [ExecuteAlways]
    public class IslandContourPreview : SerializedMonoBehaviour
    {
        [SerializeField, Required]
        private IslandGenerationContext generationContext;

        [SerializeField]
        private Color coastlineColor = new Color(0.20f, 0.90f, 0.50f, 1f);

        [SerializeField]
        private Color secondaryContourColor = new Color(1.00f, 0.85f, 0.20f, 1f);

        private Vector2[][] cachedContours;
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
            if (!TryResolveContours(out var contours))
            {
                cachedContours = null;
                cachedHash = 0;
                return;
            }

            cachedContours = contours;
            cachedHash = ComputeHash();
        }

        private void DrawPreview()
        {
            EnsureUpToDate();
            if (cachedContours == null || cachedContours.Length == 0)
            {
                return;
            }

            for (var i = 0; i < cachedContours.Length; i++)
            {
                DrawLoop(cachedContours[i], i == 0 ? coastlineColor : secondaryContourColor);
            }
        }

        private void EnsureUpToDate()
        {
            var hash = ComputeHash();
            if (cachedContours == null || cachedHash != hash)
            {
                Regenerate();
            }
        }

        private int ComputeHash()
        {
            return generationContext != null ? generationContext.GetStableHashCode() : 17;
        }

        private bool TryResolveContours(out Vector2[][] contours)
        {
            AutoBindContext();
            contours = null;
            if (generationContext == null)
            {
                Debug.LogWarning("Island contour preview requires IslandGenerationContext.", this);
                return false;
            }

            if (!generationContext.TryGetContours(out contours))
            {
                Debug.LogWarning("Island contour preview requires a valid preset in IslandGenerationContext.", this);
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

        private void DrawLoop(Vector2[] loop, Color color)
        {
            if (loop == null || loop.Length < 2)
            {
                return;
            }

            Gizmos.color = color;
            for (var i = 0; i < loop.Length - 1; i++)
            {
                var current = ToWorld(loop[i]);
                var next = ToWorld(loop[i + 1]);
                Gizmos.DrawLine(current, next);
            }
        }

        private Vector3 ToWorld(Vector2 point)
        {
            return transform.TransformPoint(new Vector3(point.x, 0f, point.y));
        }
    }
}