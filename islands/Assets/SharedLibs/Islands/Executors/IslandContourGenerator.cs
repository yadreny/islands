using UnityEngine;

namespace Islands.Generation
{
    public static class IslandContourGenerator
    {
        public static Vector2[][] GenerateClosedContours(IslandShapeRequest request, int segmentCount = 256)
        {
            var context = new IslandContourGenerationContext(request, segmentCount);
            IslandContourBuildBaseMassTask.Execute(context);
            IslandContourMacroShapeTask.Execute(context);
            IslandContourCoastlineTask.Execute(context);
            IslandContourSupplementaryContoursTask.Execute(context);
            IslandContourFinalizeTask.Execute(context);
            return context.ClosedContours.ToArray();
        }
    }

    public static class IslandContourBuildBaseMassTask
    {
        public static void Execute(IslandContourGenerationContext context)
        {
            var radii = new float[context.SegmentCount];
            for (var i = 0; i < radii.Length; i++)
            {
                var angle = IslandContourMath.Tau * i / radii.Length;
                radii[i] = IslandContourMath.EllipseRadius(angle, context.SemiMajor, context.SemiMinor);
            }

            context.OuterRadii = radii;
        }
    }

    public static class IslandContourMacroShapeTask
    {
        public static void Execute(IslandContourGenerationContext context)
        {
            ApplyMassLayoutSignature(context);
            ApplyMacroFeatures(context);
            ApplyDirectionalBias(context);
        }

        private static void ApplyMassLayoutSignature(IslandContourGenerationContext context)
        {
            var radii = context.OuterRadii;
            switch (context.Preset.MassLayoutType)
            {
                case IslandMassLayoutType.DoubleCore:
                    IslandContourMath.AddAngularBump(radii, 0f, 0.55f, 0.22f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI, 0.55f, 0.22f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.5f, 0.42f, -0.18f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 1.5f, 0.42f, -0.18f);
                    break;
                case IslandMassLayoutType.Arc:
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.15f, 0.50f, 0.12f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.85f, 0.50f, 0.12f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 1.5f, 0.70f, -0.10f);
                    break;
                case IslandMassLayoutType.ContinentalBlock:
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.2f, 0.60f, 0.08f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 1.1f, 0.60f, 0.06f);
                    break;
                case IslandMassLayoutType.BrokenBlock:
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.35f, 0.55f, 0.10f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 1.4f, 0.50f, -0.08f);
                    break;
                case IslandMassLayoutType.Ring:
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 0.25f, 0.80f, 0.04f);
                    IslandContourMath.AddAngularBump(radii, Mathf.PI * 1.25f, 0.80f, 0.04f);
                    break;
                case IslandMassLayoutType.DrownedRelief:
                    for (var i = 0; i < 3; i++)
                    {
                        IslandContourMath.AddAngularBump(radii, IslandContourMath.NextAngle(context.Random), 0.35f, -0.12f);
                    }
                    break;
            }
        }

        private static void ApplyMacroFeatures(IslandContourGenerationContext context)
        {
            var radii = context.OuterRadii;
            for (var i = 0; i < context.Preset.LobeCount; i++)
            {
                var center = IslandContourMath.NextAngle(context.Random);
                var width = Mathf.Lerp(0.28f, 0.60f, IslandContourMath.NextFloat(context.Random));
                var amplitude = context.Preset.LobeStrength * Mathf.Lerp(0.65f, 1.15f, IslandContourMath.NextFloat(context.Random));
                IslandContourMath.AddAngularBump(radii, center, width, amplitude);
            }

            for (var i = 0; i < context.Preset.BayCount; i++)
            {
                var center = IslandContourMath.NextAngle(context.Random);
                var width = Mathf.Lerp(0.20f, 0.48f, IslandContourMath.NextFloat(context.Random));
                var depth = -context.Preset.BayDepth * Mathf.Lerp(0.70f, 1.20f, IslandContourMath.NextFloat(context.Random));
                IslandContourMath.AddAngularBump(radii, center, width, depth);
            }

            if (IslandContourMath.NextFloat(context.Random) <= context.Preset.PeninsulaChance)
            {
                var peninsulaCount = context.Preset.PeninsulaChance > 0.30f ? 2 : 1;
                for (var i = 0; i < peninsulaCount; i++)
                {
                    var center = IslandContourMath.NextAngle(context.Random);
                    var amplitude = context.Preset.PeninsulaStrength * Mathf.Lerp(0.85f, 1.20f, IslandContourMath.NextFloat(context.Random));
                    IslandContourMath.AddAngularBump(radii, center, 0.16f, amplitude);
                }
            }
        }

        private static void ApplyDirectionalBias(IslandContourGenerationContext context)
        {
            var bias = context.Preset.DirectionalBias;
            if (bias <= 0.001f)
            {
                return;
            }

            var center = IslandContourMath.NextAngle(context.Random);
            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                var angle = IslandContourMath.Tau * i / context.OuterRadii.Length;
                var sector = Mathf.Cos(IslandContourMath.WrapSignedAngle(angle - center));
                var multiplier = 1f + sector * bias * 0.08f;
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i] * multiplier, 0.01f);
            }
        }
    }

    public static class IslandContourCoastlineTask
    {
        public static void Execute(IslandContourGenerationContext context)
        {
            ApplyCoastalBreakup(context);
            ClampRadii(context);
            context.OuterLoop = IslandContourMath.RadiiToLoop(context.OuterRadii);

            if (context.Preset.MassLayoutType == IslandMassLayoutType.Arc)
            {
                var bendAmount = context.Preset.ArcCurvature * context.MajorAxisLength * 0.24f;
                IslandContourMath.BendLoop(context.OuterLoop, bendAmount, context.MajorAxisLength);
            }
        }

        private static void ApplyCoastalBreakup(IslandContourGenerationContext context)
        {
            var baseStrength = context.Preset.BreakupStrength * Mathf.Lerp(0.65f, 1.55f, context.ReliefComplexity01);
            var seedA = Mathf.Abs(context.Seed * 0.0131f) + 11.3f;
            var seedB = Mathf.Abs(context.Seed * 0.0217f) + 31.7f;
            var seedC = Mathf.Abs(context.Seed * 0.0379f) + 71.1f;

            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                var angle01 = i / (float)context.OuterRadii.Length;
                var sectorMask = Mathf.Lerp(0.25f, 1f, Mathf.PerlinNoise(seedA, angle01 * 1.7f + seedB));
                var layered = 0f;
                layered += ((Mathf.PerlinNoise(seedA + angle01 * 2.3f, seedB) - 0.5f) * 2f) * 0.55f;
                layered += ((Mathf.PerlinNoise(seedB + angle01 * 5.1f, seedC) - 0.5f) * 2f) * 0.30f;
                layered += ((Mathf.PerlinNoise(seedC + angle01 * 9.7f, seedA) - 0.5f) * 2f) * 0.15f;
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i] * (1f + layered * baseStrength * sectorMask), 0.01f);
            }
        }

        private static void ClampRadii(IslandContourGenerationContext context)
        {
            var minimumRadius = Mathf.Min(context.SemiMajor, context.SemiMinor) * 0.18f;
            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i], minimumRadius);
            }
        }
    }

    public static class IslandContourSupplementaryContoursTask
    {
        public static void Execute(IslandContourGenerationContext context)
        {
            BuildInnerWaterContours(context);
            BuildOffshoreIslets(context);
        }

        private static void BuildInnerWaterContours(IslandContourGenerationContext context)
        {
            if (context.Preset.MassLayoutType != IslandMassLayoutType.Ring)
            {
                return;
            }

            var lagoonScale = Mathf.Lerp(0.42f, 0.58f, 1f - context.Preset.FootprintFill);
            var innerRadii = new float[context.OuterRadii.Length];
            for (var i = 0; i < innerRadii.Length; i++)
            {
                var angle01 = i / (float)innerRadii.Length;
                var detail = ((Mathf.PerlinNoise(context.Seed * 0.017f + angle01 * 2.1f, 17.3f) - 0.5f) * 2f) * 0.06f;
                innerRadii[i] = Mathf.Max(context.OuterRadii[i] * (lagoonScale + detail), Mathf.Min(context.SemiMajor, context.SemiMinor) * 0.08f);
            }

            context.InnerWaterContours.Add(IslandContourMath.RadiiToLoop(innerRadii));
        }

        private static void BuildOffshoreIslets(IslandContourGenerationContext context)
        {
            var baseChance = context.Preset.SatelliteChance;
            if (baseChance <= 0.001f)
            {
                return;
            }

            var count = 0;
            if (IslandContourMath.NextFloat(context.Random) <= baseChance)
            {
                count++;
            }

            if (baseChance > 0.25f && IslandContourMath.NextFloat(context.Random) <= baseChance * 0.85f)
            {
                count++;
            }

            for (var i = 0; i < count; i++)
            {
                var angle = IslandContourMath.NextAngle(context.Random);
                var sampleIndex = Mathf.Clamp(Mathf.RoundToInt(angle / IslandContourMath.Tau * context.OuterRadii.Length), 0, context.OuterRadii.Length - 1);
                var distance = context.OuterRadii[sampleIndex] * Mathf.Lerp(1.18f, 1.45f, IslandContourMath.NextFloat(context.Random));
                var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var radius = Mathf.Min(context.SemiMajor, context.SemiMinor) * Mathf.Lerp(0.06f, 0.12f, IslandContourMath.NextFloat(context.Random));
                context.OffshoreIslets.Add(IslandContourMath.CreateCircularLoop(center, radius, 20));
            }
        }
    }

    public static class IslandContourFinalizeTask
    {
        public static void Execute(IslandContourGenerationContext context)
        {
            var currentArea = Mathf.Abs(IslandContourMath.ComputePolygonArea(context.OuterLoop));
            var areaScale = currentArea > 0.0001f ? Mathf.Sqrt(context.TargetArea / currentArea) : 1f;

            IslandContourMath.ScaleLoop(context.OuterLoop, areaScale);
            IslandContourMath.ScaleLoops(context.InnerWaterContours, areaScale);
            IslandContourMath.ScaleLoops(context.OffshoreIslets, areaScale);

            IslandContourMath.RotateLoop(context.OuterLoop, context.DirectionDegrees);
            IslandContourMath.RotateLoops(context.InnerWaterContours, context.DirectionDegrees);
            IslandContourMath.RotateLoops(context.OffshoreIslets, context.DirectionDegrees);

            context.ClosedContours.Clear();
            context.ClosedContours.Add(IslandContourMath.CloseLoop(context.OuterLoop));

            for (var i = 0; i < context.InnerWaterContours.Count; i++)
            {
                context.ClosedContours.Add(IslandContourMath.CloseLoop(context.InnerWaterContours[i]));
            }

            for (var i = 0; i < context.OffshoreIslets.Count; i++)
            {
                context.ClosedContours.Add(IslandContourMath.CloseLoop(context.OffshoreIslets[i]));
            }
        }
    }
}