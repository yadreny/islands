using System;
using System.Collections.Generic;
using UnityEngine;

namespace Islands.Generation
{
    public class IslandRiverGenerator
    {
        private readonly IslandShapeRequest request;
        private readonly Vector2[][] contours;
        private readonly IslandWaterPreset waterPreset;
        private readonly int riverPointCount;
        private readonly IslandContourMath contourMath;
        private readonly System.Random random;

        public IslandRiverGenerator(IslandShapeRequest request, Vector2[][] contours, IslandWaterPreset waterPreset, int riverPointCount = 18)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.contours = contours;
            this.waterPreset = waterPreset ?? throw new ArgumentNullException(nameof(waterPreset));
            this.riverPointCount = Mathf.Clamp(riverPointCount, 6, 64);
            contourMath = new IslandContourMath();
            random = new System.Random(request.Seed ^ 0x45d9f3b);
        }

        public Vector4[][] Execute()
        {
            if (request.Preset == null)
            {
                return Array.Empty<Vector4[]>();
            }

            if (contours == null || contours.Length == 0 || contours[0] == null || contours[0].Length < 4)
            {
                return Array.Empty<Vector4[]>();
            }

            var coastline = OpenLoop(contours[0]);
            var riverCount = ResolveRiverCount(coastline);
            if (riverCount <= 0)
            {
                return Array.Empty<Vector4[]>();
            }

            var centroid = ComputeCentroid(coastline);
            var bounds = ComputeBounds(coastline);
            var islandScale = Mathf.Max(1f, Mathf.Min(bounds.width, bounds.height));
            var acceptedRivers = new List<Vector4[]>(riverCount);
            var acceptedMouths = new List<Vector2>(riverCount);
            var baseOffset = contourMath.NextFloat(random) * coastline.Length;
            var indexStep = coastline.Length / (float)riverCount;
            var maxAttemptsPerRiver = 8;

            for (var i = 0; i < riverCount; i++)
            {
                for (var attempt = 0; attempt < maxAttemptsPerRiver; attempt++)
                {
                    var jitterScale = waterPreset.MouthJitter + attempt * 0.03f;
                    var jitter = Mathf.Lerp(-jitterScale, jitterScale, contourMath.NextFloat(random)) * indexStep;
                    var mouthIndex = Mathf.RoundToInt((baseOffset + i * indexStep + jitter) % coastline.Length);
                    if (mouthIndex < 0)
                    {
                        mouthIndex += coastline.Length;
                    }

                    var candidate = BuildRiver(coastline, mouthIndex, centroid, islandScale);
                    if (candidate == null || candidate.Length <= 1)
                    {
                        continue;
                    }

                    var mouth = GetRiverPoint(candidate[candidate.Length - 1]);
                    if (IsTooCloseToExistingMouths(mouth, acceptedMouths, islandScale * waterPreset.MouthSpacing))
                    {
                        continue;
                    }

                    if (TryFindBranchJoin(candidate, acceptedRivers, out var join))
                    {
                        candidate = BuildBranchRiver(join, candidate);
                    }

                    acceptedRivers.Add(candidate);
                    acceptedMouths.Add(mouth);
                    break;
                }
            }

            return acceptedRivers.ToArray();
        }

        private Vector4[] BuildRiver(Vector2[] coastline, int mouthIndex, Vector2 centroid, float islandScale)
        {
            var mouth = coastline[mouthIndex];
            var inward = ComputeInwardNormal(coastline, mouthIndex);
            var tangent = ComputeTangent(coastline, mouthIndex);
            var reliefFactor = Mathf.Lerp(0.95f, 1.30f, ResolveRelief01());

            var uplandDistance = islandScale * Mathf.Lerp(Mathf.Max(0.20f, waterPreset.InlandReach - 0.14f), waterPreset.InlandReach + 0.10f, contourMath.NextFloat(random)) * reliefFactor;
            var lateralOffset = islandScale * Mathf.Lerp(-waterPreset.MeanderStrength, waterPreset.MeanderStrength, contourMath.NextFloat(random));
            var innerAnchor = mouth + inward * uplandDistance;
            var sourceCandidate = Vector2.Lerp(centroid, innerAnchor, 0.60f) + tangent * lateralOffset;
            var source = PullInsidePolygon(coastline, centroid, sourceCandidate, 12);

            var minimumLength = islandScale * waterPreset.MinimumRiverLength;
            var currentLength = Vector2.Distance(source, mouth);
            if (currentLength < minimumLength)
            {
                var fallback = mouth + inward * minimumLength + tangent * lateralOffset * 0.5f;
                source = PullInsidePolygon(coastline, centroid, fallback, 12);
                currentLength = Vector2.Distance(source, mouth);
            }

            if (currentLength < islandScale * 0.16f)
            {
                return null;
            }

            var flowDirection = (mouth - source).sqrMagnitude > 0.0001f ? (mouth - source).normalized : -inward;
            var bendSign = contourMath.NextFloat(random) < 0.5f ? -1f : 1f;
            var bendMagnitude = islandScale * Mathf.Lerp(waterPreset.MeanderStrength * 0.35f, waterPreset.MeanderStrength, contourMath.NextFloat(random));
            var controlA = Vector2.Lerp(source, mouth, 0.30f) + tangent * bendMagnitude * bendSign + flowDirection * islandScale * 0.015f;
            var controlB = Vector2.Lerp(source, mouth, 0.72f) + tangent * bendMagnitude * bendSign * 0.50f;

            var points = new Vector2[riverPointCount];
            for (var i = 0; i < riverPointCount; i++)
            {
                var t = riverPointCount == 1 ? 1f : i / (float)(riverPointCount - 1);
                var point = EvaluateCubicBezier(source, controlA, controlB, mouth, t);
                if (i < riverPointCount - 1)
                {
                    point = PullInsidePolygon(coastline, centroid, point, 2);
                }
                else
                {
                    point = mouth;
                }

                points[i] = point;
            }

            SmoothRiverPoints(points, coastline, centroid, 4, waterPreset.SmoothingStrength);
            points[0] = PullInsidePolygon(coastline, centroid, points[0], 3);
            points[points.Length - 1] = mouth;

            var mouthWidth = islandScale * Mathf.Lerp(0.010f, 0.026f, waterPreset.RiverAbundance) * Mathf.Lerp(0.9f, 1.2f, contourMath.NextFloat(random));
            var sourceWidth = mouthWidth * Mathf.Lerp(0.18f, 0.30f, contourMath.NextFloat(random));
            var river = new Vector4[riverPointCount];
            for (var i = 0; i < riverPointCount; i++)
            {
                var t = riverPointCount == 1 ? 1f : i / (float)(riverPointCount - 1);
                var width = Mathf.Lerp(sourceWidth, mouthWidth, Mathf.Pow(t, 1.10f));
                river[i] = new Vector4(points[i].x, 0f, points[i].y, width);
            }

            return river;
        }

        private Vector4[] BuildBranchRiver(RiverJoinInfo join, Vector4[] candidate)
        {
            var merged = new List<Vector4>(join.ExistingRiver.Length + candidate.Length);

            for (var i = 0; i < join.ExistingSegmentIndex; i++)
            {
                merged.Add(join.ExistingRiver[i]);
            }

            var existingWidth = Mathf.Lerp(join.ExistingRiver[join.ExistingSegmentIndex].w, join.ExistingRiver[join.ExistingSegmentIndex + 1].w, join.ExistingSegmentT);
            var candidateWidth = Mathf.Lerp(candidate[join.CandidateSegmentIndex].w, candidate[join.CandidateSegmentIndex + 1].w, join.CandidateSegmentT);
            var joinWidth = Mathf.Max(existingWidth, candidateWidth);
            var joinPoint = new Vector4(join.Point.x, 0f, join.Point.y, joinWidth);

            if (merged.Count == 0 || Vector2.Distance(GetRiverPoint(merged[merged.Count - 1]), join.Point) > 0.0001f)
            {
                merged.Add(joinPoint);
            }

            for (var i = join.CandidateSegmentIndex + 1; i < candidate.Length; i++)
            {
                merged.Add(candidate[i]);
            }

            return merged.ToArray();
        }

        private bool TryFindBranchJoin(Vector4[] candidate, List<Vector4[]> acceptedRivers, out RiverJoinInfo join)
        {
            join = default;
            var found = false;
            var bestCandidateProgress = float.MinValue;

            for (var riverIndex = 0; riverIndex < acceptedRivers.Count; riverIndex++)
            {
                var existing = acceptedRivers[riverIndex];
                for (var i = 0; i < candidate.Length - 1; i++)
                {
                    var a0 = GetRiverPoint(candidate[i]);
                    var a1 = GetRiverPoint(candidate[i + 1]);
                    for (var j = 0; j < existing.Length - 1; j++)
                    {
                        var b0 = GetRiverPoint(existing[j]);
                        var b1 = GetRiverPoint(existing[j + 1]);
                        if (!TryGetSegmentIntersection(a0, a1, b0, b1, out var point, out var ta, out var tb))
                        {
                            continue;
                        }

                        var candidateProgress = (i + ta) / Mathf.Max(1f, candidate.Length - 1f);
                        if (!found || candidateProgress > bestCandidateProgress)
                        {
                            found = true;
                            bestCandidateProgress = candidateProgress;
                            join = new RiverJoinInfo(existing, j, tb, i, ta, point);
                        }
                    }
                }
            }

            return found;
        }

        private bool TryGetSegmentIntersection(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 point, out float ta, out float tb)
        {
            point = default;
            ta = 0f;
            tb = 0f;

            var r = a1 - a0;
            var s = b1 - b0;
            var denominator = Cross(r, s);
            if (Mathf.Abs(denominator) <= 0.00001f)
            {
                return false;
            }

            var diff = b0 - a0;
            ta = Cross(diff, s) / denominator;
            tb = Cross(diff, r) / denominator;
            if (ta < 0f || ta > 1f || tb < 0f || tb > 1f)
            {
                return false;
            }

            point = a0 + r * ta;
            return true;
        }

        private float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private void SmoothRiverPoints(Vector2[] points, Vector2[] coastline, Vector2 centroid, int passes, float strength)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            var buffer = new Vector2[points.Length];
            for (var pass = 0; pass < passes; pass++)
            {
                buffer[0] = points[0];
                buffer[points.Length - 1] = points[points.Length - 1];
                for (var i = 1; i < points.Length - 1; i++)
                {
                    var average = (points[i - 1] + points[i + 1]) * 0.5f;
                    var smoothed = Vector2.Lerp(points[i], average, strength);
                    buffer[i] = PullInsidePolygon(coastline, centroid, smoothed, 2);
                }

                Array.Copy(buffer, points, points.Length);
            }
        }

        private bool IsTooCloseToExistingMouths(Vector2 candidateMouth, List<Vector2> acceptedMouths, float minDistance)
        {
            for (var i = 0; i < acceptedMouths.Count; i++)
            {
                if (Vector2.Distance(candidateMouth, acceptedMouths[i]) < minDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 GetRiverPoint(Vector4 value)
        {
            return new Vector2(value.x, value.z);
        }

        private int ResolveRiverCount(Vector2[] coastline)
        {
            var areaFactor = Mathf.Sqrt(Mathf.Max(1f, request.TargetArea) / Mathf.Max(1f, request.Preset.RecommendedArea));
            var sizeFactor = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(areaFactor));
            var reliefFactor = Mathf.Lerp(0.85f, 1.25f, ResolveRelief01());
            var baseCount = Mathf.Lerp(0f, 8f, waterPreset.RiverAbundance);
            var count = Mathf.RoundToInt(baseCount * sizeFactor * reliefFactor);
            if (waterPreset.RiverAbundance > 0.08f && count == 0 && coastline.Length > 0)
            {
                count = 1;
            }

            return Mathf.Clamp(count, 0, 12);
        }

        private float ResolveRelief01()
        {
            var finalRelief = request.Preset.RecommendedReliefComplexity * request.ReliefComplexityPercent / 100f;
            return Mathf.InverseLerp(0.05f, 1.5f, Mathf.Clamp(finalRelief, 0.05f, 1.5f));
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

        private Vector2 ComputeCentroid(Vector2[] polygon)
        {
            var sum = Vector2.zero;
            for (var i = 0; i < polygon.Length; i++)
            {
                sum += polygon[i];
            }

            return polygon.Length > 0 ? sum / polygon.Length : Vector2.zero;
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

        private Vector2 ComputeTangent(Vector2[] loop, int index)
        {
            var prev = loop[(index - 1 + loop.Length) % loop.Length];
            var next = loop[(index + 1) % loop.Length];
            var tangent = next - prev;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.right;
        }

        private Vector2 ComputeInwardNormal(Vector2[] loop, int index)
        {
            var tangent = ComputeTangent(loop, index);
            var signedArea = contourMath.ComputePolygonArea(loop);
            var outward = signedArea >= 0f
                ? new Vector2(tangent.y, -tangent.x)
                : new Vector2(-tangent.y, tangent.x);
            return -outward.normalized;
        }

        private Vector2 PullInsidePolygon(Vector2[] polygon, Vector2 centroid, Vector2 point, int maxIterations)
        {
            if (IsPointInsidePolygon(polygon, point))
            {
                return point;
            }

            var current = point;
            for (var i = 0; i < maxIterations; i++)
            {
                current = Vector2.Lerp(current, centroid, 0.25f);
                if (IsPointInsidePolygon(polygon, current))
                {
                    return current;
                }
            }

            return centroid;
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

        private Vector2 EvaluateCubicBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a +
                   3f * u * u * t * b +
                   3f * u * t * t * c +
                   t * t * t * d;
        }

        private readonly struct RiverJoinInfo
        {
            public RiverJoinInfo(Vector4[] existingRiver, int existingSegmentIndex, float existingSegmentT, int candidateSegmentIndex, float candidateSegmentT, Vector2 point)
            {
                ExistingRiver = existingRiver;
                ExistingSegmentIndex = existingSegmentIndex;
                ExistingSegmentT = existingSegmentT;
                CandidateSegmentIndex = candidateSegmentIndex;
                CandidateSegmentT = candidateSegmentT;
                Point = point;
            }

            public Vector4[] ExistingRiver { get; }
            public int ExistingSegmentIndex { get; }
            public float ExistingSegmentT { get; }
            public int CandidateSegmentIndex { get; }
            public float CandidateSegmentT { get; }
            public Vector2 Point { get; }
        }
    }
}