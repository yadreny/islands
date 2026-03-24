using System;
using System.Collections.Generic;
using UnityEngine;

namespace Islands.Generation
{
    public sealed class IslandContourGenerationContext
    {
        public IslandContourGenerationContext(IslandShapeRequest request, int segmentCount)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Preset = request.Preset != null ? request.Preset : throw new InvalidOperationException("IslandShapeRequest requires a preset asset.");
            SegmentCount = Mathf.Clamp(segmentCount, 64, 1024);
            Seed = request.Seed;
            Random = new System.Random(Seed);

            TargetArea = Mathf.Max(1f, request.TargetArea);
            TargetMaxElevation = Mathf.Max(0.1f, request.TargetMaxElevation);
            FinalAspectRatio = 1f + (Preset.RecommendedAspectRatio - 1f) * request.AspectPercent / 100f;
            FinalAspectRatio = Mathf.Max(1f, FinalAspectRatio);
            FinalReliefComplexity = Preset.RecommendedReliefComplexity * request.ReliefComplexityPercent / 100f;
            FinalReliefComplexity = Mathf.Clamp(FinalReliefComplexity, 0.05f, 1.5f);
            FinalCoastlineComplexity = Mathf.Max(0f, request.CoastlineComplexity);

            MajorAxisLength = Mathf.Sqrt(TargetArea * FinalAspectRatio / Preset.FootprintFill);
            MinorAxisLength = Mathf.Sqrt(TargetArea / (FinalAspectRatio * Preset.FootprintFill));
            SemiMajor = MajorAxisLength * 0.5f;
            SemiMinor = MinorAxisLength * 0.5f;
            DirectionDegrees = request.Direction;
            OffshoreIsletCount = Mathf.Max(0, request.OffshoreIsletCount);
        }

        public IslandShapeRequest Request { get; }
        public IslandShapePreset Preset { get; }
        public int SegmentCount { get; }
        public int Seed { get; }
        public System.Random Random { get; }
        public float TargetArea { get; }
        public float TargetMaxElevation { get; }
        public float FinalAspectRatio { get; }
        public float FinalReliefComplexity { get; }
        public float FinalCoastlineComplexity { get; }
        public float MajorAxisLength { get; }
        public float MinorAxisLength { get; }
        public float SemiMajor { get; }
        public float SemiMinor { get; }
        public float DirectionDegrees { get; }
        public int OffshoreIsletCount { get; }
        public float ReliefComplexity01 => Mathf.InverseLerp(0.05f, 1.5f, FinalReliefComplexity);

        public float[] OuterRadii { get; set; }
        public Vector2[] OuterLoop { get; set; }
        public List<Vector2[]> InnerWaterContours { get; } = new List<Vector2[]>();
        public List<Vector2[]> OffshoreIslets { get; } = new List<Vector2[]>();
        public List<Vector2[]> ClosedContours { get; } = new List<Vector2[]>();
    }

    public class IslandContourMath
    {
        public float Tau => Mathf.PI * 2f;

        public float EllipseRadius(float angle, float semiMajor, float semiMinor)
        {
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var denominator = Mathf.Sqrt((semiMinor * cos * semiMinor * cos) + (semiMajor * sin * semiMajor * sin));
            if (denominator <= 0.0001f)
            {
                return Mathf.Min(semiMajor, semiMinor);
            }

            return (semiMajor * semiMinor) / denominator;
        }

        public void AddAngularBump(float[] radii, float centerAngle, float sigma, float amplitude)
        {
            sigma = Mathf.Max(0.01f, sigma);
            for (var i = 0; i < radii.Length; i++)
            {
                var angle = Tau * i / radii.Length;
                var delta = WrapSignedAngle(angle - centerAngle);
                var weight = Mathf.Exp(-(delta * delta) / (2f * sigma * sigma));
                radii[i] = Mathf.Max(radii[i] * (1f + amplitude * weight), 0.01f);
            }
        }

        public float WrapSignedAngle(float angle)
        {
            while (angle > Mathf.PI)
            {
                angle -= Tau;
            }

            while (angle < -Mathf.PI)
            {
                angle += Tau;
            }

            return angle;
        }

        public float NextAngle(System.Random random)
        {
            return NextFloat(random) * Tau;
        }

        public float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }

        public float SamplePeriodicNoise(float seedX, float seedY, float angle01, float frequency, float radius = 1f)
        {
            var angle = angle01 * Tau * frequency;
            var sampleX = seedX + Mathf.Cos(angle) * radius;
            var sampleY = seedY + Mathf.Sin(angle) * radius;
            return Mathf.PerlinNoise(sampleX, sampleY);
        }

        public float SignedPow(float value, float exponent)
        {
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }

        public Vector2[] RadiiToLoop(float[] radii)
        {
            var loop = new Vector2[radii.Length];
            for (var i = 0; i < radii.Length; i++)
            {
                var angle = Tau * i / radii.Length;
                loop[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radii[i];
            }

            return loop;
        }

        public Vector2[] CreateCircularLoop(Vector2 center, float radius, int segments)
        {
            var loop = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = Tau * i / segments;
                loop[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return loop;
        }

        public Vector2[] CreateOrganicIsletLoop(
            Vector2 center,
            float baseRadius,
            int segments,
            System.Random random,
            int seed,
            IslandShapePreset preset)
        {
            var aspectTarget = Mathf.Clamp(preset.RecommendedAspectRatio, 1.02f, 2.2f);
            var aspectBlend = preset.MassLayoutType == IslandMassLayoutType.Arc ? 0.85f : 0.55f;
            var aspectRatio = Mathf.Lerp(1.03f, aspectTarget, aspectBlend);
            aspectRatio *= Mathf.Lerp(0.92f, 1.08f, NextFloat(random));
            aspectRatio = Mathf.Clamp(aspectRatio, 1.02f, 2.1f);

            var semiMajor = baseRadius * aspectRatio;
            var semiMinor = baseRadius / aspectRatio;
            var radii = new float[segments];

            for (var i = 0; i < segments; i++)
            {
                var angle = Tau * i / segments;
                radii[i] = EllipseRadius(angle, semiMajor, semiMinor);
            }

            var lobeCount = Mathf.Clamp(Mathf.RoundToInt(preset.LobeCount * 0.5f), 0, 3);
            var bayCount = Mathf.Clamp(Mathf.RoundToInt(preset.BayCount * 0.5f), 0, 2);

            switch (preset.MassLayoutType)
            {
                case IslandMassLayoutType.BrokenBlock:
                case IslandMassLayoutType.DrownedRelief:
                    lobeCount = Mathf.Clamp(lobeCount + 1, 1, 3);
                    bayCount = Mathf.Clamp(bayCount + 1, 0, 2);
                    break;
                case IslandMassLayoutType.Ring:
                    lobeCount = Mathf.Min(lobeCount, 1);
                    bayCount = 0;
                    break;
                case IslandMassLayoutType.Arc:
                    lobeCount = Mathf.Max(lobeCount, 1);
                    break;
            }

            for (var i = 0; i < lobeCount; i++)
            {
                AddAngularBump(
                    radii,
                    NextAngle(random),
                    Mathf.Lerp(0.28f, 0.65f, NextFloat(random)),
                    Mathf.Max(0.03f, preset.LobeStrength * Mathf.Lerp(0.35f, 0.75f, NextFloat(random))));
            }

            for (var i = 0; i < bayCount; i++)
            {
                AddAngularBump(
                    radii,
                    NextAngle(random),
                    Mathf.Lerp(0.18f, 0.40f, NextFloat(random)),
                    -Mathf.Max(0.02f, preset.BayDepth * Mathf.Lerp(0.30f, 0.70f, NextFloat(random))));
            }

            var loop = RadiiToLoop(radii);
            RotateLoop(loop, NextFloat(random) * 360f + (seed % 29));
            for (var i = 0; i < loop.Length; i++)
            {
                loop[i] += center;
            }

            return loop;
        }

        public void ApplyNormalBreakup(Vector2[] loop, float amplitude, int seed, bool useLocalSafety = true)
        {
            if (loop == null || loop.Length < 3 || amplitude <= 0.0001f)
            {
                return;
            }

            var original = new Vector2[loop.Length];
            Array.Copy(loop, original, loop.Length);

            var safeAmplitude = useLocalSafety ? Mathf.Min(amplitude, ComputeSafeNormalBreakupAmplitude(original)) : amplitude;
            if (safeAmplitude <= 0.0001f)
            {
                return;
            }

            var fractions = BuildLoopFractions(original);
            var normals = BuildOutwardNormals(original);
            var displacements = new float[loop.Length];
            var seedA = Mathf.Abs(seed * 0.0131f) + 11.3f;
            var seedB = Mathf.Abs(seed * 0.0217f) + 31.7f;
            var seedC = Mathf.Abs(seed * 0.0379f) + 71.1f;
            var seedD = Mathf.Abs(seed * 0.0513f) + 97.7f;

            for (var i = 0; i < loop.Length; i++)
            {
                var distance01 = fractions[i];
                var sectorMask = Mathf.Lerp(0.35f, 1f, SamplePeriodicNoise(seedA, seedB, distance01, 2f));
                var roughnessMask = Mathf.Lerp(0.65f, 1.35f, SamplePeriodicNoise(seedC, seedD, distance01, 3f));

                var low = (SamplePeriodicNoise(seedA + 13.1f, seedB + 7.7f, distance01, 3f) - 0.5f) * 2f;
                var mid = SignedPow((SamplePeriodicNoise(seedB + 19.3f, seedC + 5.1f, distance01, 5f) - 0.5f) * 2f, 1.20f);
                var high = SignedPow((SamplePeriodicNoise(seedC + 23.7f, seedA + 3.9f, distance01, 10f) - 0.5f) * 2f, 1.45f);
                var micro = SignedPow((SamplePeriodicNoise(seedD + 29.9f, seedB + 2.7f, distance01, 18f) - 0.5f) * 2f, 1.80f);
                var ultra = SignedPow((SamplePeriodicNoise(seedA + 41.3f, seedD + 6.9f, distance01, 28f) - 0.5f) * 2f, 2.20f);

                var layered = 0f;
                layered += low * 0.32f;
                layered += mid * 0.28f;
                layered += high * 0.20f;
                layered += micro * 0.14f;
                layered += ultra * 0.06f;

                var displacement = safeAmplitude * sectorMask * roughnessMask * layered;
                var maxOffset = safeAmplitude * 1.10f;
                displacements[i] = Mathf.Clamp(displacement, -maxOffset, maxOffset);
            }

            SmoothRingValues(displacements, 3);

            for (var i = 0; i < loop.Length; i++)
            {
                loop[i] = original[i] + normals[i] * displacements[i];
            }
        }

        public void ScaleLoop(Vector2[] loop, float scale)
        {
            for (var i = 0; i < loop.Length; i++)
            {
                loop[i] *= scale;
            }
        }

        public void ScaleLoops(List<Vector2[]> loops, float scale)
        {
            for (var i = 0; i < loops.Count; i++)
            {
                ScaleLoop(loops[i], scale);
            }
        }

        public void RotateLoop(Vector2[] loop, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            for (var i = 0; i < loop.Length; i++)
            {
                var point = loop[i];
                loop[i] = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
            }
        }

        public void RotateLoops(List<Vector2[]> loops, float degrees)
        {
            for (var i = 0; i < loops.Count; i++)
            {
                RotateLoop(loops[i], degrees);
            }
        }

        public void BendLoop(Vector2[] loop, float bendAmount, float majorAxisLength)
        {
            var halfMajor = Mathf.Max(0.001f, majorAxisLength * 0.5f);
            for (var i = 0; i < loop.Length; i++)
            {
                var normalizedX = Mathf.Clamp(loop[i].x / halfMajor, -1f, 1f);
                var bend = bendAmount * ((normalizedX * normalizedX) - 0.35f);
                loop[i] = new Vector2(loop[i].x, loop[i].y + bend);
            }
        }

        public float ComputePolygonArea(Vector2[] polygon)
        {
            var sum = 0f;
            for (var i = 0; i < polygon.Length; i++)
            {
                var next = polygon[(i + 1) % polygon.Length];
                sum += polygon[i].x * next.y - next.x * polygon[i].y;
            }

            return sum * 0.5f;
        }

        public Rect ComputeBounds(IReadOnlyList<Vector2[]> loops)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < loops.Count; i++)
            {
                ExpandBounds(loops[i], ref min, ref max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public Vector2[] CloseLoop(Vector2[] loop)
        {
            if (loop == null || loop.Length == 0)
            {
                return loop;
            }

            var closed = new Vector2[loop.Length + 1];
            for (var i = 0; i < loop.Length; i++)
            {
                closed[i] = loop[i];
            }

            closed[closed.Length - 1] = loop[0];
            return closed;
        }

        private float[] BuildLoopFractions(Vector2[] loop)
        {
            var fractions = new float[loop.Length];
            var totalLength = 0f;
            for (var i = 0; i < loop.Length; i++)
            {
                var next = loop[(i + 1) % loop.Length];
                totalLength += Vector2.Distance(loop[i], next);
            }

            if (totalLength <= 0.0001f)
            {
                return fractions;
            }

            var accumulated = 0f;
            for (var i = 0; i < loop.Length; i++)
            {
                fractions[i] = accumulated / totalLength;
                var next = loop[(i + 1) % loop.Length];
                accumulated += Vector2.Distance(loop[i], next);
            }

            return fractions;
        }

        private float ComputeSafeNormalBreakupAmplitude(Vector2[] loop)
        {
            var minEdgeLength = float.MaxValue;
            var totalEdgeLength = 0f;
            for (var i = 0; i < loop.Length; i++)
            {
                var next = loop[(i + 1) % loop.Length];
                var edgeLength = Vector2.Distance(loop[i], next);
                minEdgeLength = Mathf.Min(minEdgeLength, edgeLength);
                totalEdgeLength += edgeLength;
            }

            var averageEdgeLength = totalEdgeLength / Mathf.Max(1, loop.Length);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            ExpandBounds(loop, ref min, ref max);
            var bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var minExtent = Mathf.Min(bounds.width, bounds.height);

            var edgeCap = minEdgeLength * 0.30f;
            var averageCap = averageEdgeLength * 0.42f;
            var extentCap = minExtent * 0.08f;
            return Mathf.Max(0.01f, Mathf.Min(edgeCap, averageCap, extentCap));
        }

        private Vector2[] BuildOutwardNormals(Vector2[] loop)
        {
            var normals = new Vector2[loop.Length];
            var signedArea = ComputePolygonArea(loop);
            var isCounterClockwise = signedArea >= 0f;

            for (var i = 0; i < loop.Length; i++)
            {
                var prev = loop[(i - 1 + loop.Length) % loop.Length];
                var next = loop[(i + 1) % loop.Length];
                var tangent = next - prev;
                if (tangent.sqrMagnitude <= 0.000001f)
                {
                    tangent = loop[i] - prev;
                }

                tangent = tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector2.right;
                normals[i] = isCounterClockwise
                    ? new Vector2(tangent.y, -tangent.x)
                    : new Vector2(-tangent.y, tangent.x);
            }

            return normals;
        }

        private void SmoothRingValues(float[] values, int passes)
        {
            if (values == null || values.Length < 3)
            {
                return;
            }

            var buffer = new float[values.Length];
            for (var pass = 0; pass < passes; pass++)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var prev = values[(i - 1 + values.Length) % values.Length];
                    var current = values[i];
                    var next = values[(i + 1) % values.Length];
                    buffer[i] = prev * 0.25f + current * 0.50f + next * 0.25f;
                }

                Array.Copy(buffer, values, values.Length);
            }
        }

        private void ExpandBounds(Vector2[] loop, ref Vector2 min, ref Vector2 max)
        {
            for (var i = 0; i < loop.Length; i++)
            {
                min = Vector2.Min(min, loop[i]);
                max = Vector2.Max(max, loop[i]);
            }
        }
    }
}