using System;
using System.Collections.Generic;
using UnityEngine;

namespace Islands.Generation
{
    public class IslandRiverGenerator
    {
        private readonly IslandShapePreset shapePreset;
        private readonly IslandShapeRequest request;
        private readonly Vector2[][] contours;
        private readonly IslandWaterPreset waterPreset;
        private readonly System.Random random;

        public IslandRiverGenerator(IslandShapePreset shapePreset, IslandShapeRequest request, Vector2[][] contours, IslandWaterPreset waterPreset)
        {
            this.shapePreset = shapePreset ?? throw new ArgumentNullException(nameof(shapePreset));
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.contours = contours;
            this.waterPreset = waterPreset ?? throw new ArgumentNullException(nameof(waterPreset));
            random = new System.Random(request.Seed ^ 0x45d9f3b);
        }

        public Vector4[][] Execute()
        {
            if (contours == null || contours.Length == 0 || contours[0] == null || contours[0].Length < 4)
            {
                return Array.Empty<Vector4[]>();
            }

            if (waterPreset.FlowSources <= 0)
            {
                return Array.Empty<Vector4[]>();
            }

            var coastline = OpenLoop(contours[0]);
            var bounds = ComputeBounds(coastline);
            var centroid = ComputeCentroid(coastline);
            var islandScale = Mathf.Max(1f, Mathf.Min(bounds.width, bounds.height));
            var mouthSpacing = islandScale * 0.06f;
            var acceptedRivers = new List<Vector4[]>();
            var acceptedMouths = new List<Vector2>();
            var sources = BuildSources(coastline, bounds, centroid, islandScale);

            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var source = sources[sourceIndex];
                var mouth = FindClosestCoastPoint(source, coastline, acceptedMouths, mouthSpacing);
                if (!mouth.HasValue)
                {
                    continue;
                }

                var river = BuildDirectRiver(source, mouth.Value, coastline, centroid, islandScale, sourceIndex);
                if (river == null || river.Length < 2)
                {
                    continue;
                }

                acceptedRivers.Add(river);
                acceptedMouths.Add(mouth.Value);
            }

            return acceptedRivers.ToArray();
        }

        private List<Vector2> BuildSources(Vector2[] coastline, Rect bounds, Vector2 centroid, float islandScale)
        {
            var sources = new List<Vector2>(waterPreset.FlowSources);
            var minSpacing = islandScale * 0.14f;
            var minCoastClearance = islandScale * 0.12f;
            var attempts = Mathf.Max(64, waterPreset.FlowSources * 60);

            for (var attempt = 0; attempt < attempts && sources.Count < waterPreset.FlowSources; attempt++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, (float)random.NextDouble()),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, (float)random.NextDouble()));

                if (!TryBuildValidSource(coastline, centroid, candidate, minCoastClearance, out var source))
                {
                    continue;
                }

                if (IsTooCloseToExistingPoints(sources, source, minSpacing))
                {
                    continue;
                }

                sources.Add(source);
            }

            return sources;
        }

        private Vector2? FindClosestCoastPoint(Vector2 source, Vector2[] coastline, List<Vector2> acceptedMouths, float mouthSpacing)
        {
            Vector2? bestPoint = null;
            var bestDistanceSq = float.MaxValue;

            for (var i = 0; i < coastline.Length; i++)
            {
                var a = coastline[i];
                var b = coastline[(i + 1) % coastline.Length];
                var candidate = ClosestPointOnSegment(source, a, b);
                if (IsTooCloseToExistingPoints(acceptedMouths, candidate, mouthSpacing))
                {
                    continue;
                }

                var distanceSq = (candidate - source).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestPoint = candidate;
                }
            }

            return bestPoint;
        }

        private Vector4[] BuildDirectRiver(Vector2 source, Vector2 mouth, Vector2[] coastline, Vector2 centroid, float islandScale, int widthSeedOffset)
        {
            var points = BuildMeanderPoints(source, mouth, coastline, centroid);
            if (points == null || points.Length < 2)
            {
                return null;
            }

            var mouthWidth = islandScale * Mathf.Lerp(0.010f, 0.026f, Mathf.Clamp01(waterPreset.FlowSources / 8f));
            var widthRandom = new System.Random(request.Seed + widthSeedOffset);
            var sourceWidth = mouthWidth * Mathf.Lerp(0.18f, 0.26f, (float)widthRandom.NextDouble());
            return BuildWidthProfile(points, sourceWidth, mouthWidth);
        }

        private Vector2[] BuildMeanderPoints(Vector2 source, Vector2 mouth, Vector2[] coastline, Vector2 centroid)
        {
            var axis = mouth - source;
            var totalLength = axis.magnitude;
            if (totalLength <= 0.0001f)
            {
                return new[] { source, mouth };
            }

            var points = new List<Vector2>();
            points.Add(source);

            var direction = axis / totalLength;
            var normal = new Vector2(-direction.y, direction.x);
            var halfLength = Mathf.Max(0.05f, waterPreset.MeanderLength * 0.5f);
            var halfWidth = waterPreset.MeanderWidth * 0.5f;
            var maxInteriorPoints = Mathf.Max(0, waterPreset.RiverPointCount - 2);
            var progress = halfLength;
            var side = 1f;

            while (progress < totalLength && points.Count - 1 < maxInteriorPoints)
            {
                var pointOnAxis = source + direction * progress;
                var meanderPoint = pointOnAxis + normal * (halfWidth * side);
                points.Add(meanderPoint);
                progress += halfLength;
                side *= -1f;
            }

            points.Add(mouth);
            return points.ToArray();
        }

        private Vector4[] BuildWidthProfile(Vector2[] points, float sourceWidth, float mouthWidth)
        {
            var river = new Vector4[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var t = points.Length == 1 ? 1f : i / (float)(points.Length - 1);
                var width = Mathf.Lerp(sourceWidth, mouthWidth, t);
                river[i] = new Vector4(points[i].x, 0f, points[i].y, width);
            }

            return river;
        }

        private Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var abLengthSq = ab.sqrMagnitude;
            if (abLengthSq <= 0.000001f)
            {
                return a;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLengthSq);
            return a + ab * t;
        }

        private bool IsTooCloseToExistingPoints(List<Vector2> points, Vector2 candidate, float minDistance)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (Vector2.Distance(candidate, points[i]) < minDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2[] OpenLoop(Vector2[] closedLoop)
        {
            if (closedLoop == null || closedLoop.Length <= 1)
            {
                return Array.Empty<Vector2>();
            }

            var length = closedLoop.Length;
            if (closedLoop[0] == closedLoop[length - 1])
            {
                length -= 1;
            }

            var open = new Vector2[length];
            Array.Copy(closedLoop, open, length);
            return open;
        }

        private Rect ComputeBounds(Vector2[] loop)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < loop.Length; i++)
            {
                min = Vector2.Min(min, loop[i]);
                max = Vector2.Max(max, loop[i]);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ComputeCentroid(Vector2[] polygon)
        {
            var sum = Vector2.zero;
            for (var i = 0; i < polygon.Length; i++)
            {
                sum += polygon[i];
            }

            return polygon.Length > 0 ? sum / polygon.Length : Vector2.zero;
        }

        private float ComputeDistanceToCoast(Vector2 point, Vector2[] coastline)
        {
            var bestDistance = float.MaxValue;
            for (var i = 0; i < coastline.Length; i++)
            {
                var a = coastline[i];
                var b = coastline[(i + 1) % coastline.Length];
                var closest = ClosestPointOnSegment(point, a, b);
                var distance = Vector2.Distance(point, closest);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                }
            }

            return bestDistance;
        }

        private bool TryBuildValidSource(Vector2[] coastline, Vector2 centroid, Vector2 candidate, float minCoastClearance, out Vector2 source)
        {
            source = centroid;
            var current = candidate;
            for (var i = 0; i < 8; i++)
            {
                current = Vector2.Lerp(current, centroid, 0.25f);
                if (IsPointInsidePolygon(coastline, current) && ComputeDistanceToCoast(current, coastline) >= minCoastClearance)
                {
                    return TryProjectToSafeInterior(coastline, centroid, current, minCoastClearance, out source);
                }
            }

            return TryProjectToSafeInterior(coastline, centroid, current, minCoastClearance, out source);
        }

        private bool TryProjectToSafeInterior(Vector2[] coastline, Vector2 centroid, Vector2 candidate, float minCoastClearance, out Vector2 source)
        {
            source = centroid;
            var low = 0f;
            var high = 1f;
            var found = false;
            var best = centroid;

            for (var i = 0; i < 20; i++)
            {
                var t = (low + high) * 0.5f;
                var point = Vector2.Lerp(centroid, candidate, t);
                var isInside = IsPointInsidePolygon(coastline, point);
                var hasClearance = isInside && ComputeDistanceToCoast(point, coastline) >= minCoastClearance;
                if (hasClearance)
                {
                    found = true;
                    best = point;
                    low = t;
                }
                else
                {
                    high = t;
                }
            }

            source = best;
            return found;
        }

        private bool IsPointInsidePolygon(Vector2[] polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                var intersects = ((a.y > point.y) != (b.y > point.y)) &&
                                 (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.00001f, b.y - a.y) + a.x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}