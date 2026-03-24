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
        private readonly int riverPointCount;
        private readonly IslandContourMath contourMath;
        private readonly System.Random random;

        public IslandRiverGenerator(IslandShapePreset shapePreset, IslandShapeRequest request, Vector2[][] contours, IslandWaterPreset waterPreset)
        {
            this.shapePreset = shapePreset ?? throw new ArgumentNullException(nameof(shapePreset));
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.contours = contours;
            this.waterPreset = waterPreset ?? throw new ArgumentNullException(nameof(waterPreset));
            this.riverPointCount = Mathf.Clamp(riverPointCount, 6, 64);
            contourMath = new IslandContourMath();
            random = new System.Random(request.Seed ^ 0x45d9f3b);
        }

        public Vector4[][] Execute()
        {
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
                    var jitterScale = waterPreset.MouthJitter + attempt * 0.02f;
                    var jitter = Mathf.Lerp(-jitterScale, jitterScale, contourMath.NextFloat(random)) * indexStep;
                    var mouthIndex = Mathf.RoundToInt((baseOffset + i * indexStep + jitter) % coastline.Length);
                    if (mouthIndex < 0)
                    {
                        mouthIndex += coastline.Length;
                    }

                    var mouth = coastline[mouthIndex];
                    if (IsTooCloseToExistingMouths(mouth, acceptedMouths, islandScale * waterPreset.MouthSpacing))
                    {
                        continue;
                    }

                    Vector4[] candidate = null;
                    var allowBranch = acceptedRivers.Count > 0 && contourMath.NextFloat(random) < waterPreset.BranchChance;
                    if (allowBranch)
                    {
                        candidate = BuildBranchRiver(acceptedRivers, coastline, mouthIndex, centroid, islandScale);
                    }

                    candidate ??= BuildTrunkRiver(coastline, mouthIndex, centroid, islandScale);
                    if (candidate == null || candidate.Length <= 1)
                    {
                        continue;
                    }

                    acceptedRivers.Add(candidate);
                    acceptedMouths.Add(mouth);
                    break;
                }
            }

            return acceptedRivers.ToArray();
        }

        private Vector4[] BuildTrunkRiver(Vector2[] coastline, int mouthIndex, Vector2 centroid, float islandScale)
        {
            var mouth = coastline[mouthIndex];
            var inward = ComputeInwardNormal(coastline, mouthIndex);
            var tangent = ComputeTangent(coastline, mouthIndex);
            var reliefFactor = Mathf.Lerp(0.95f, 1.30f, ResolveRelief01());
            var inlandDistance = islandScale * Mathf.Lerp(Mathf.Max(0.24f, waterPreset.InlandReach - 0.08f), waterPreset.InlandReach + 0.12f, contourMath.NextFloat(random)) * reliefFactor;
            var lateralOffset = islandScale * Mathf.Lerp(-waterPreset.MeanderStrength, waterPreset.MeanderStrength, contourMath.NextFloat(random)) * 0.55f;
            var sourceAnchor = mouth + inward * inlandDistance + tangent * lateralOffset;
            var sourceCandidate = Vector2.Lerp(centroid, sourceAnchor, 0.58f);
            var source = PullInsidePolygon(coastline, centroid, sourceCandidate, 12);

            var minimumLength = islandScale * waterPreset.MinimumRiverLength;
            if (Vector2.Distance(source, mouth) < minimumLength)
            {
                var fallback = mouth + inward * minimumLength + tangent * lateralOffset * 0.5f;
                source = PullInsidePolygon(coastline, centroid, fallback, 12);
            }

            if (Vector2.Distance(source, mouth) < islandScale * 0.16f)
            {
                return null;
            }

            var distance = Vector2.Distance(source, mouth);
            var mouthTangent = tangent;
            var flowDirection = (mouth - source).sqrMagnitude > 0.0001f ? (mouth - source).normalized : -inward;
            var bendSign = contourMath.NextFloat(random) < 0.5f ? -1f : 1f;
            var bendMagnitude = distance * Mathf.Lerp(waterPreset.MeanderStrength * 0.20f, waterPreset.MeanderStrength * 0.55f, contourMath.NextFloat(random));
            var controlA = source + flowDirection * distance * 0.28f + new Vector2(-flowDirection.y, flowDirection.x) * bendMagnitude * bendSign;
            var controlB = mouth - inward * distance * 0.18f + mouthTangent * bendMagnitude * bendSign * 0.35f;

            var points = BuildBezierPoints(source, controlA, controlB, mouth, coastline, centroid, waterPreset.RiverPointCount);
            if (points == null)
            {
                return null;
            }

            SmoothRiverPoints(points, coastline, centroid, 5, waterPreset.SmoothingStrength);
            points[0] = PullInsidePolygon(coastline, centroid, points[0], 3);
            points[points.Length - 1] = mouth;

            var mouthWidth = islandScale * Mathf.Lerp(0.010f, 0.026f, waterPreset.RiverAbundance) * Mathf.Lerp(0.9f, 1.15f, contourMath.NextFloat(random));
            var sourceWidth = mouthWidth * Mathf.Lerp(0.16f, 0.24f, contourMath.NextFloat(random));
            return BuildWidthProfile(points, sourceWidth, mouthWidth, 1.08f);
        }

        private Vector4[] BuildBranchRiver(List<Vector4[]> acceptedRivers, Vector2[] coastline, int mouthIndex, Vector2 centroid, float islandScale)
        {
            var parentRiver = SelectBranchParent(acceptedRivers, coastline[mouthIndex]);
            if (parentRiver == null || parentRiver.Length < 4)
            {
                return null;
            }

            var branchProgress = Mathf.Lerp(waterPreset.BranchingStart, 0.86f, contourMath.NextFloat(random));
            var branchSample = SampleRiver(parentRiver, branchProgress);
            var branchPoint = branchSample.Point;
            var downstreamTangent = branchSample.Tangent.sqrMagnitude > 0.0001f ? branchSample.Tangent.normalized : Vector2.down;
            var branchWidth = branchSample.Width;
            var mouth = coastline[mouthIndex];
            var mouthTangent = ComputeTangent(coastline, mouthIndex);
            var inward = ComputeInwardNormal(coastline, mouthIndex);
            var toMouth = mouth - branchPoint;
            var distance = toMouth.magnitude;
            if (distance < islandScale * 0.12f)
            {
                return null;
            }

            var bendNormal = new Vector2(-downstreamTangent.y, downstreamTangent.x);
            var branchSign = Mathf.Sign(Vector2.Dot(bendNormal, toMouth));
            if (Mathf.Abs(branchSign) < 0.5f)
            {
                branchSign = contourMath.NextFloat(random) < 0.5f ? -1f : 1f;
            }

            var bendMagnitude = distance * Mathf.Lerp(waterPreset.MeanderStrength * 0.12f, waterPreset.MeanderStrength * 0.40f, contourMath.NextFloat(random));
            var controlA = branchPoint + downstreamTangent * distance * 0.24f + bendNormal * bendMagnitude * branchSign;
            var controlB = mouth - inward * distance * 0.16f + mouthTangent * bendMagnitude * branchSign * 0.30f;
            var tailPointCount = Mathf.Max(6, waterPreset.RiverPointCount / 2 + 2);
            var tailPoints = BuildBezierPoints(branchPoint, controlA, controlB, mouth, coastline, centroid, tailPointCount);
            if (tailPoints == null)
            {
                return null;
            }

            SmoothRiverPoints(tailPoints, coastline, centroid, 4, waterPreset.SmoothingStrength);
            tailPoints[0] = branchPoint;
            tailPoints[tailPoints.Length - 1] = mouth;

            var prefix = ExtractPrefix(parentRiver, branchProgress, branchPoint, branchWidth);
            if (prefix.Count == 0)
            {
                return null;
            }

            var tailStartWidth = branchWidth * Mathf.Lerp(0.92f, 0.98f, contourMath.NextFloat(random));
            var tailMouthWidth = branchWidth * Mathf.Lerp(0.72f, 0.90f, contourMath.NextFloat(random));
            var tail = BuildWidthProfile(tailPoints, tailStartWidth, tailMouthWidth, 0.95f);

            var merged = new List<Vector4>(prefix.Count + tail.Length);
            merged.AddRange(prefix);
            for (var i = 1; i < tail.Length; i++)
            {
                merged.Add(tail[i]);
            }

            return merged.ToArray();
        }

        private Vector4[] SelectBranchParent(List<Vector4[]> rivers, Vector2 mouth)
        {
            Vector4[] bestRiver = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < rivers.Count; i++)
            {
                var river = rivers[i];
                var riverMouth = GetRiverPoint(river[river.Length - 1]);
                var distance = Vector2.Distance(riverMouth, mouth);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRiver = river;
                }
            }

            return bestRiver;
        }

        private RiverSample SampleRiver(Vector4[] river, float progress)
        {
            progress = Mathf.Clamp01(progress);
            var scaled = progress * (river.Length - 1);
            var index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, river.Length - 2);
            var t = scaled - index;
            var a = river[index];
            var b = river[index + 1];
            var pointA = GetRiverPoint(a);
            var pointB = GetRiverPoint(b);
            var point = Vector2.Lerp(pointA, pointB, t);
            var tangent = pointB - pointA;
            var width = Mathf.Lerp(a.w, b.w, t);
            return new RiverSample(point, tangent, width);
        }

        private List<Vector4> ExtractPrefix(Vector4[] river, float progress, Vector2 branchPoint, float branchWidth)
        {
            progress = Mathf.Clamp01(progress);
            var scaled = progress * (river.Length - 1);
            var index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, river.Length - 2);
            var prefix = new List<Vector4>(index + 2);
            for (var i = 0; i <= index; i++)
            {
                prefix.Add(river[i]);
            }

            var lastPoint = prefix.Count > 0 ? GetRiverPoint(prefix[prefix.Count - 1]) : Vector2.positiveInfinity;
            if (!Approximately(lastPoint, branchPoint))
            {
                prefix.Add(new Vector4(branchPoint.x, 0f, branchPoint.y, branchWidth));
            }
            else
            {
                prefix[prefix.Count - 1] = new Vector4(branchPoint.x, 0f, branchPoint.y, branchWidth);
            }

            return prefix;
        }

        private Vector2[] BuildBezierPoints(Vector2 start, Vector2 controlA, Vector2 controlB, Vector2 end, Vector2[] coastline, Vector2 centroid, int pointCount)
        {
            if (pointCount < 2)
            {
                return null;
            }

            var points = new Vector2[pointCount];
            for (var i = 0; i < pointCount; i++)
            {
                var t = pointCount == 1 ? 1f : i / (float)(pointCount - 1);
                var point = EvaluateCubicBezier(start, controlA, controlB, end, t);
                if (i > 0 && i < pointCount - 1)
                {
                    point = PullInsidePolygon(coastline, centroid, point, 3);
                }
                else if (i == pointCount - 1)
                {
                    point = end;
                }

                points[i] = point;
            }

            return points;
        }

        private Vector4[] BuildWidthProfile(Vector2[] points, float startWidth, float endWidth, float power)
        {
            var river = new Vector4[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var t = points.Length == 1 ? 1f : i / (float)(points.Length - 1);
                var width = Mathf.Lerp(startWidth, endWidth, Mathf.Pow(t, power));
                river[i] = new Vector4(points[i].x, 0f, points[i].y, width);
            }

            return river;
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
            var areaFactor = Mathf.Sqrt(Mathf.Max(1f, request.TargetArea) / Mathf.Max(1f, shapePreset.RecommendedArea));
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
            var finalRelief = shapePreset.RecommendedReliefComplexity * request.ReliefComplexityPercent / 100f;
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

        private bool Approximately(Vector2 a, Vector2 b)
        {
            return Vector2.SqrMagnitude(a - b) <= 0.000001f;
        }

        private readonly struct RiverSample
        {
            public RiverSample(Vector2 point, Vector2 tangent, float width)
            {
                Point = point;
                Tangent = tangent;
                Width = width;
            }

            public Vector2 Point { get; }
            public Vector2 Tangent { get; }
            public float Width { get; }
        }
    }
}