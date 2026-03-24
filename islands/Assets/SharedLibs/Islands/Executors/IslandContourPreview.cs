using Sirenix.OdinInspector;
using UnityEngine;

namespace Islands.Generation
{
    [ExecuteAlways]
    public class IslandContourPreview : SerializedMonoBehaviour
    {
        [SerializeField, InlineProperty, HideLabel]
        private IslandShapeRequest request = new IslandShapeRequest();

        [SerializeField, Range(64, 1024)]
        private int contourSegments = 256;

        [SerializeField]
        private bool regenerateAutomatically = true;

        [SerializeField]
        private float gizmoHeight = 0f;

        [SerializeField]
        private bool drawWhenNotSelected;

        [SerializeField]
        private bool drawBounds = true;

        [SerializeField]
        private Color coastlineColor = new Color(0.20f, 0.90f, 0.50f, 1f);

        [SerializeField]
        private Color secondaryContourColor = new Color(1.00f, 0.85f, 0.20f, 1f);

        [SerializeField]
        private Color boundsColor = new Color(1.00f, 0.40f, 0.20f, 1f);

        private Vector2[][] cachedContours;
        private Rect cachedBounds;
        private int cachedHash;

        [Button(ButtonSizes.Large)]
        public void Regenerate()
        {
            if (request == null)
            {
                request = new IslandShapeRequest();
            }

            if (request.Preset == null)
            {
                Debug.LogWarning("Island contour preview requires a preset asset.", this);
                cachedContours = null;
                cachedHash = 0;
                return;
            }

            cachedContours = IslandContourGenerator.GenerateClosedContours(request, contourSegments);
            cachedBounds = cachedContours != null && cachedContours.Length > 0 ? IslandContourMath.ComputeBounds(cachedContours) : default;
            cachedHash = ComputeHash();
        }

        private void OnEnable()
        {
            if (regenerateAutomatically)
            {
                Regenerate();
            }
        }

        private void OnValidate()
        {
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
            if (cachedContours == null || cachedContours.Length == 0)
            {
                return;
            }

            for (var i = 0; i < cachedContours.Length; i++)
            {
                DrawLoop(cachedContours[i], i == 0 ? coastlineColor : secondaryContourColor);
            }

            if (drawBounds)
            {
                DrawBounds(cachedBounds);
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
            unchecked
            {
                var hash = request != null ? request.GetStableHashCode() : 17;
                hash = hash * 31 + contourSegments;
                return hash;
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

        private void DrawBounds(Rect rect)
        {
            Gizmos.color = boundsColor;
            var a = ToWorld(new Vector2(rect.xMin, rect.yMin));
            var b = ToWorld(new Vector2(rect.xMax, rect.yMin));
            var c = ToWorld(new Vector2(rect.xMax, rect.yMax));
            var d = ToWorld(new Vector2(rect.xMin, rect.yMax));
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        private Vector3 ToWorld(Vector2 point)
        {
            return transform.TransformPoint(new Vector3(point.x, gizmoHeight, point.y));
        }
    }
}