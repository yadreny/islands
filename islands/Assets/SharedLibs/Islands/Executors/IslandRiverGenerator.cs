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

            if (waterPreset.MergeRivers)
            {
                ResolveRiverIntersections(acceptedRivers);
            }

            if (waterPreset.MergeShortRivers)
            {
                ResolveShortRivers(acceptedRivers);
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
                var progress01 = Mathf.Clamp01(progress / totalLength);
                var widthScale = Mathf.Lerp(0.35f, 1f, progress01);
                var meanderPoint = pointOnAxis + normal * (halfWidth * widthScale * side);
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

        private void ResolveRiverIntersections(List<Vector4[]> rivers)
        {
            if (rivers == null || rivers.Count < 2)
            {
                return;
            }

            var changed = true;
            var guard = 0;
            while (changed && guard < 8)
            {
                changed = false;
                guard++;
                var order = BuildRiverOrderByLength(rivers, descending: true);

                for (var orderIndex = 0; orderIndex < order.Count; orderIndex++)
                {
                    var mainIndex = order[orderIndex];
                    var mainRiver = rivers[mainIndex];
                    if (mainRiver == null || mainRiver.Length < 2)
                    {
                        continue;
                    }

                    for (var otherOrderIndex = orderIndex + 1; otherOrderIndex < order.Count; otherOrderIndex++)
                    {
                        var secondaryIndex = order[otherOrderIndex];
                        var secondaryRiver = rivers[secondaryIndex];
                        if (secondaryRiver == null || secondaryRiver.Length < 2)
                        {
                            continue;
                        }

                        if (!TryFindConnectionFromMouth(mainRiver, secondaryRiver, waterPreset.MergeSnapDistance, out var intersection, out var secondarySegmentIndex, out var secondarySegmentT))
                        {
                            continue;
                        }

                        var trimmed = TrimRiverToBranch(secondaryRiver, intersection, secondarySegmentIndex, secondarySegmentT);
                        if (trimmed == null || trimmed.Length < 2)
                        {
                            continue;
                        }

                        rivers[secondaryIndex] = trimmed;
                        changed = true;
                    }
                }
            }
        }

        private void ResolveShortRivers(List<Vector4[]> rivers)
        {
            if (rivers == null || rivers.Count == 0)
            {
                return;
            }

            var order = BuildRiverOrderByLength(rivers, descending: true);
            for (var orderIndex = 0; orderIndex < order.Count; orderIndex++)
            {
                var shortIndex = order[orderIndex];
                var shortRiver = rivers[shortIndex];
                if (shortRiver == null || shortRiver.Length < 2)
                {
                    continue;
                }

                if (ComputeSourceToMouthLength(shortRiver) >= waterPreset.ShortRiverMinLength)
                {
                    continue;
                }

                if (TryFindClosestRiverConnection(shortIndex, rivers, waterPreset.MergeShortDistance, out var intersection, out var shortSegmentIndex, out var shortSegmentT))
                {
                    var trimmed = TrimRiverToBranch(shortRiver, intersection, shortSegmentIndex, shortSegmentT);
                    rivers[shortIndex] = trimmed != null && trimmed.Length >= 2 ? trimmed : Array.Empty<Vector4>();
                }
                else
                {
                    rivers[shortIndex] = Array.Empty<Vector4>();
                }
            }
        }

        private List<int> BuildRiverOrderByLength(List<Vector4[]> rivers, bool descending)
        {
            var order = new List<int>(rivers.Count);
            for (var i = 0; i < rivers.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((left, right) =>
            {
                var leftLength = ComputeSourceToMouthLength(rivers[left]);
                var rightLength = ComputeSourceToMouthLength(rivers[right]);
                return descending
                    ? rightLength.CompareTo(leftLength)
                    : leftLength.CompareTo(rightLength);
            });
            return order;
        }

        private bool TryFindClosestRiverConnection(int sourceRiverIndex, List<Vector4[]> rivers, float maxDistance, out Vector2 intersection, out int sourceSegmentIndex, out float sourceSegmentT)
        {
            intersection = Vector2.zero;
            sourceSegmentIndex = -1;
            sourceSegmentT = 0f;
            var sourceRiver = rivers[sourceRiverIndex];
            var bestDistance = float.MaxValue;
            var found = false;

            for (var otherIndex = 0; otherIndex < rivers.Count; otherIndex++)
            {
                if (otherIndex == sourceRiverIndex)
                {
                    continue;
                }

                var otherRiver = rivers[otherIndex];
                if (otherRiver == null || otherRiver.Length < 2)
                {
                    continue;
                }

                for (var sourceIndex = 0; sourceIndex < sourceRiver.Length - 1; sourceIndex++)
                {
                    var sourceA = ToPlanar(sourceRiver[sourceIndex]);
                    var sourceB = ToPlanar(sourceRiver[sourceIndex + 1]);
                    for (var otherSegmentIndex = 0; otherSegmentIndex < otherRiver.Length - 1; otherSegmentIndex++)
                    {
                        var otherA = ToPlanar(otherRiver[otherSegmentIndex]);
                        var otherB = ToPlanar(otherRiver[otherSegmentIndex + 1]);
                        ClosestPointsBetweenSegments(otherA, otherB, sourceA, sourceB, out var pointOnOther, out var pointOnSource, out _, out var tOnSource);
                        var distance = Vector2.Distance(pointOnOther, pointOnSource);
                        if (distance > maxDistance || distance >= bestDistance)
                        {
                            continue;
                        }

                        bestDistance = distance;
                        intersection = pointOnOther;
                        sourceSegmentIndex = sourceIndex;
                        sourceSegmentT = tOnSource;
                        found = true;
                    }
                }
            }

            return found;
        }

        private float ComputeSourceToMouthLength(Vector4[] river)
        {
            if (river == null || river.Length < 2)
            {
                return 0f;
            }

            return Vector2.Distance(ToPlanar(river[0]), ToPlanar(river[river.Length - 1]));
        }

        private bool TryFindConnectionFromMouth(Vector4[] mainRiver, Vector4[] secondaryRiver, float snapDistance, out Vector2 intersection, out int secondarySegmentIndex, out float secondarySegmentT)
        {
            intersection = Vector2.zero;
            secondarySegmentIndex = -1;
            secondarySegmentT = 0f;

            for (var mainIndex = mainRiver.Length - 2; mainIndex >= 0; mainIndex--)
            {
                var mainA = ToPlanar(mainRiver[mainIndex]);
                var mainB = ToPlanar(mainRiver[mainIndex + 1]);
                var bestCandidate = default(ConnectionCandidate);
                var foundCandidate = false;

                for (var secondaryIndex = 0; secondaryIndex < secondaryRiver.Length - 1; secondaryIndex++)
                {
                    var secondaryA = ToPlanar(secondaryRiver[secondaryIndex]);
                    var secondaryB = ToPlanar(secondaryRiver[secondaryIndex + 1]);

                    if (TryIntersectSegments(mainA, mainB, secondaryA, secondaryB, out var intersectionPoint, out _, out var tSecondary))
                    {
                        var candidate = new ConnectionCandidate(intersectionPoint, secondaryIndex, tSecondary, 0f);
                        if (!foundCandidate || candidate.Distance < bestCandidate.Distance)
                        {
                            bestCandidate = candidate;
                            foundCandidate = true;
                        }

                        continue;
                    }

                    if (!TryFindNearSegmentConnection(mainA, mainB, secondaryA, secondaryB, snapDistance, out var nearPoint, out var nearTSecondary, out var nearDistance))
                    {
                        continue;
                    }

                    var nearCandidate = new ConnectionCandidate(nearPoint, secondaryIndex, nearTSecondary, nearDistance);
                    if (!foundCandidate || nearCandidate.Distance < bestCandidate.Distance)
                    {
                        bestCandidate = nearCandidate;
                        foundCandidate = true;
                    }
                }

                if (foundCandidate)
                {
                    intersection = bestCandidate.Intersection;
                    secondarySegmentIndex = bestCandidate.SecondarySegmentIndex;
                    secondarySegmentT = bestCandidate.SecondarySegmentT;
                    return true;
                }
            }

            return false;
        }

        private Vector4[] TrimRiverToBranch(Vector4[] river, Vector2 intersection, int segmentIndex, float segmentT)
        {
            if (river == null || river.Length < 2 || segmentIndex < 0 || segmentIndex >= river.Length - 1)
            {
                return river;
            }

            var width = Mathf.Lerp(river[segmentIndex].w, river[segmentIndex + 1].w, segmentT);
            var trimmed = new List<Vector4>(river.Length - segmentIndex + 1)
            {
                new Vector4(intersection.x, 0f, intersection.y, width)
            };

            for (var i = segmentIndex + 1; i < river.Length; i++)
            {
                trimmed.Add(river[i]);
            }

            return trimmed.ToArray();
        }

        private bool TryIntersectSegments(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection, out float tA, out float tB)
        {
            intersection = Vector2.zero;
            tA = 0f;
            tB = 0f;

            var r = a2 - a1;
            var s = b2 - b1;
            var denominator = Cross(r, s);
            if (Mathf.Abs(denominator) <= 0.000001f)
            {
                return false;
            }

            var delta = b1 - a1;
            tA = Cross(delta, s) / denominator;
            tB = Cross(delta, r) / denominator;
            if (tA < 0f || tA > 1f || tB < 0f || tB > 1f)
            {
                return false;
            }

            if ((tA <= 0.0001f || tA >= 0.9999f) && (tB <= 0.0001f || tB >= 0.9999f))
            {
                return false;
            }

            intersection = a1 + r * tA;
            return true;
        }

        private bool TryFindNearSegmentConnection(Vector2 mainA, Vector2 mainB, Vector2 secondaryA, Vector2 secondaryB, float snapDistance, out Vector2 intersection, out float secondaryT, out float distance)
        {
            intersection = Vector2.zero;
            secondaryT = 0f;
            distance = float.MaxValue;

            ClosestPointsBetweenSegments(mainA, mainB, secondaryA, secondaryB, out var pointOnMain, out var pointOnSecondary, out _, out var tOnSecondary);
            distance = Vector2.Distance(pointOnMain, pointOnSecondary);
            if (distance > snapDistance)
            {
                return false;
            }

            intersection = pointOnMain;
            secondaryT = tOnSecondary;
            return true;
        }

        private void ClosestPointsBetweenSegments(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 pointOnA, out Vector2 pointOnB, out float tA, out float tB)
        {
            var d1 = a1 - a0;
            var d2 = b1 - b0;
            var r = a0 - b0;
            var a = Vector2.Dot(d1, d1);
            var e = Vector2.Dot(d2, d2);
            var f = Vector2.Dot(d2, r);

            if (a <= 0.000001f && e <= 0.000001f)
            {
                tA = 0f;
                tB = 0f;
                pointOnA = a0;
                pointOnB = b0;
                return;
            }

            if (a <= 0.000001f)
            {
                tA = 0f;
                tB = Mathf.Clamp01(f / e);
            }
            else
            {
                var c = Vector2.Dot(d1, r);
                if (e <= 0.000001f)
                {
                    tB = 0f;
                    tA = Mathf.Clamp01(-c / a);
                }
                else
                {
                    var b = Vector2.Dot(d1, d2);
                    var denom = a * e - b * b;
                    if (Mathf.Abs(denom) > 0.000001f)
                    {
                        tA = Mathf.Clamp01((b * f - c * e) / denom);
                    }
                    else
                    {
                        tA = 0f;
                    }

                    tB = (b * tA + f) / e;
                    if (tB < 0f)
                    {
                        tB = 0f;
                        tA = Mathf.Clamp01(-c / a);
                    }
                    else if (tB > 1f)
                    {
                        tB = 1f;
                        tA = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            pointOnA = a0 + d1 * tA;
            pointOnB = b0 + d2 * tB;
        }

        private float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private Vector2 ToPlanar(Vector4 point)
        {
            return new Vector2(point.x, point.z);
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

        private readonly struct ConnectionCandidate
        {
            public ConnectionCandidate(Vector2 intersection, int secondarySegmentIndex, float secondarySegmentT, float distance)
            {
                Intersection = intersection;
                SecondarySegmentIndex = secondarySegmentIndex;
                SecondarySegmentT = secondarySegmentT;
                Distance = distance;
            }

            public Vector2 Intersection { get; }
            public int SecondarySegmentIndex { get; }
            public float SecondarySegmentT { get; }
            public float Distance { get; }
        }
    }
}