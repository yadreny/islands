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

            MajorAxisLength = Mathf.Sqrt(TargetArea * FinalAspectRatio / Preset.FootprintFill);
            MinorAxisLength = Mathf.Sqrt(TargetArea / (FinalAspectRatio * Preset.FootprintFill));
            SemiMajor = MajorAxisLength * 0.5f;
            SemiMinor = MinorAxisLength * 0.5f;
            DirectionDegrees = request.Direction;
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
        public float MajorAxisLength { get; }
        public float MinorAxisLength { get; }
        public float SemiMajor { get; }
        public float SemiMinor { get; }
        public float DirectionDegrees { get; }
        public float ReliefComplexity01 => Mathf.InverseLerp(0.05f, 1.5f, FinalReliefComplexity);

        public float[] OuterRadii { get; set; }
        public Vector2[] OuterLoop { get; set; }
        public List<Vector2[]> InnerWaterContours { get; } = new List<Vector2[]>();
        public List<Vector2[]> OffshoreIslets { get; } = new List<Vector2[]>();
        public List<Vector2[]> ClosedContours { get; } = new List<Vector2[]>();
    }

    public static class IslandContourMath
    {
        public const float Tau = Mathf.PI * 2f;

        public static float EllipseRadius(float angle, float semiMajor, float semiMinor)
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

        public static void AddAngularBump(float[] radii, float centerAngle, float sigma, float amplitude)
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

        public static float WrapSignedAngle(float angle)
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

        public static float NextAngle(System.Random random)
        {
            return NextFloat(random) * Tau;
        }

        public static float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }

        public static Vector2[] RadiiToLoop(float[] radii)
        {
            var loop = new Vector2[radii.Length];
            for (var i = 0; i < radii.Length; i++)
            {
                var angle = Tau * i / radii.Length;
                loop[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radii[i];
            }

            return loop;
        }

        public static Vector2[] CreateCircularLoop(Vector2 center, float radius, int segments)
        {
            var loop = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = Tau * i / segments;
                loop[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return loop;
        }

        public static void ScaleLoop(Vector2[] loop, float scale)
        {
            for (var i = 0; i < loop.Length; i++)
            {
                loop[i] *= scale;
            }
        }

        public static void ScaleLoops(List<Vector2[]> loops, float scale)
        {
            for (var i = 0; i < loops.Count; i++)
            {
                ScaleLoop(loops[i], scale);
            }
        }

        public static void RotateLoop(Vector2[] loop, float degrees)
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

        public static void RotateLoops(List<Vector2[]> loops, float degrees)
        {
            for (var i = 0; i < loops.Count; i++)
            {
                RotateLoop(loops[i], degrees);
            }
        }

        public static void BendLoop(Vector2[] loop, float bendAmount, float majorAxisLength)
        {
            var halfMajor = Mathf.Max(0.001f, majorAxisLength * 0.5f);
            for (var i = 0; i < loop.Length; i++)
            {
                var normalizedX = Mathf.Clamp(loop[i].x / halfMajor, -1f, 1f);
                var bend = bendAmount * ((normalizedX * normalizedX) - 0.35f);
                loop[i] = new Vector2(loop[i].x, loop[i].y + bend);
            }
        }

        public static float ComputePolygonArea(Vector2[] polygon)
        {
            var sum = 0f;
            for (var i = 0; i < polygon.Length; i++)
            {
                var next = polygon[(i + 1) % polygon.Length];
                sum += polygon[i].x * next.y - next.x * polygon[i].y;
            }

            return sum * 0.5f;
        }

        public static Rect ComputeBounds(IReadOnlyList<Vector2[]> loops)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < loops.Count; i++)
            {
                ExpandBounds(loops[i], ref min, ref max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Vector2[] CloseLoop(Vector2[] loop)
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

        private static void ExpandBounds(Vector2[] loop, ref Vector2 min, ref Vector2 max)
        {
            for (var i = 0; i < loop.Length; i++)
            {
                min = Vector2.Min(min, loop[i]);
                max = Vector2.Max(max, loop[i]);
            }
        }
    }
}