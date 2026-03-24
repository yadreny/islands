using UnityEngine;

namespace Islands.Generation
{
    public class IslandContourGenerator
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourGenerator(IslandShapeRequest request, int segmentCount = 256)
        {
            context = new IslandContourGenerationContext(request, segmentCount);
            contourMath = new IslandContourMath();
        }

        public Vector2[][] Execute()
        {
            new IslandContourBuildBaseMassTask(context, contourMath).Execute();
            new IslandContourMacroShapeTask(context, contourMath).Execute();
            new IslandContourCoastlineTask(context, contourMath).Execute();
            new IslandContourSupplementaryContoursTask(context, contourMath).Execute();
            new IslandContourFinalizeTask(context, contourMath).Execute();
            return context.ClosedContours.ToArray();
        }
    }

    public class IslandContourBuildBaseMassTask
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourBuildBaseMassTask(IslandContourGenerationContext context, IslandContourMath contourMath)
        {
            this.context = context;
            this.contourMath = contourMath;
        }

        public void Execute()
        {
            var radii = new float[context.SegmentCount];
            for (var i = 0; i < radii.Length; i++)
            {
                var angle = contourMath.Tau * i / radii.Length;
                radii[i] = contourMath.EllipseRadius(angle, context.SemiMajor, context.SemiMinor);
            }

            context.OuterRadii = radii;
        }
    }

    public class IslandContourMacroShapeTask
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourMacroShapeTask(IslandContourGenerationContext context, IslandContourMath contourMath)
        {
            this.context = context;
            this.contourMath = contourMath;
        }

        public void Execute()
        {
            ApplyMassLayoutSignature();
            ApplyMacroFeatures();
            ApplyDirectionalBias();
        }

        private void ApplyMassLayoutSignature()
        {
            var radii = context.OuterRadii;
            switch (context.Preset.MassLayoutType)
            {
                case IslandMassLayoutType.DoubleCore:
                    contourMath.AddAngularBump(radii, 0f, 0.55f, 0.22f);
                    contourMath.AddAngularBump(radii, Mathf.PI, 0.55f, 0.22f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.5f, 0.42f, -0.18f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 1.5f, 0.42f, -0.18f);
                    break;
                case IslandMassLayoutType.Arc:
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.15f, 0.50f, 0.12f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.85f, 0.50f, 0.12f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 1.5f, 0.70f, -0.10f);
                    break;
                case IslandMassLayoutType.ContinentalBlock:
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.2f, 0.60f, 0.08f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 1.1f, 0.60f, 0.06f);
                    break;
                case IslandMassLayoutType.BrokenBlock:
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.35f, 0.55f, 0.10f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 1.4f, 0.50f, -0.08f);
                    break;
                case IslandMassLayoutType.Ring:
                    contourMath.AddAngularBump(radii, Mathf.PI * 0.25f, 0.80f, 0.04f);
                    contourMath.AddAngularBump(radii, Mathf.PI * 1.25f, 0.80f, 0.04f);
                    break;
                case IslandMassLayoutType.DrownedRelief:
                    for (var i = 0; i < 3; i++)
                    {
                        contourMath.AddAngularBump(radii, contourMath.NextAngle(context.Random), 0.35f, -0.12f);
                    }
                    break;
            }
        }

        private void ApplyMacroFeatures()
        {
            var radii = context.OuterRadii;
            for (var i = 0; i < context.Preset.LobeCount; i++)
            {
                var center = contourMath.NextAngle(context.Random);
                var width = Mathf.Lerp(0.28f, 0.60f, contourMath.NextFloat(context.Random));
                var amplitude = context.Preset.LobeStrength * Mathf.Lerp(0.65f, 1.15f, contourMath.NextFloat(context.Random));
                contourMath.AddAngularBump(radii, center, width, amplitude);
            }

            for (var i = 0; i < context.Preset.BayCount; i++)
            {
                var center = contourMath.NextAngle(context.Random);
                var width = Mathf.Lerp(0.20f, 0.48f, contourMath.NextFloat(context.Random));
                var depth = -context.Preset.BayDepth * Mathf.Lerp(0.70f, 1.20f, contourMath.NextFloat(context.Random));
                contourMath.AddAngularBump(radii, center, width, depth);
            }

            if (contourMath.NextFloat(context.Random) <= context.Preset.PeninsulaChance)
            {
                var peninsulaCount = context.Preset.PeninsulaChance > 0.30f ? 2 : 1;
                for (var i = 0; i < peninsulaCount; i++)
                {
                    var center = contourMath.NextAngle(context.Random);
                    var amplitude = context.Preset.PeninsulaStrength * Mathf.Lerp(0.85f, 1.20f, contourMath.NextFloat(context.Random));
                    contourMath.AddAngularBump(radii, center, 0.16f, amplitude);
                }
            }
        }

        private void ApplyDirectionalBias()
        {
            var bias = context.Preset.DirectionalBias;
            if (bias <= 0.001f)
            {
                return;
            }

            var center = contourMath.NextAngle(context.Random);
            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                var angle = contourMath.Tau * i / context.OuterRadii.Length;
                var sector = Mathf.Cos(contourMath.WrapSignedAngle(angle - center));
                var multiplier = 1f + sector * bias * 0.08f;
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i] * multiplier, 0.01f);
            }
        }
    }

    public class IslandContourCoastlineTask
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourCoastlineTask(IslandContourGenerationContext context, IslandContourMath contourMath)
        {
            this.context = context;
            this.contourMath = contourMath;
        }

        public void Execute()
        {
            ApplyCoastalBreakup();
            ClampRadii();
            context.OuterLoop = contourMath.RadiiToLoop(context.OuterRadii);

            if (context.Preset.MassLayoutType == IslandMassLayoutType.Arc)
            {
                var bendAmount = context.Preset.ArcCurvature * context.MajorAxisLength * 0.24f;
                contourMath.BendLoop(context.OuterLoop, bendAmount, context.MajorAxisLength);
            }
        }

        private void ApplyCoastalBreakup()
        {
            if (context.FinalCoastlineComplexity <= 0.0001f)
            {
                return;
            }

            var reliefInfluence = Mathf.Lerp(0.90f, 1.95f, context.ReliefComplexity01);
            var baseStrength = context.FinalCoastlineComplexity * 1.85f * reliefInfluence;
            var seedA = Mathf.Abs(context.Seed * 0.0131f) + 11.3f;
            var seedB = Mathf.Abs(context.Seed * 0.0217f) + 31.7f;
            var seedC = Mathf.Abs(context.Seed * 0.0379f) + 71.1f;
            var seedD = Mathf.Abs(context.Seed * 0.0513f) + 97.7f;

            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                var angle01 = i / (float)context.OuterRadii.Length;
                var sectorMask = Mathf.Lerp(0.20f, 1f, contourMath.SamplePeriodicNoise(seedA, seedB, angle01, 2f));
                var roughnessMask = Mathf.Lerp(0.45f, 1.55f, contourMath.SamplePeriodicNoise(seedC, seedD, angle01, 3f));

                var low = (contourMath.SamplePeriodicNoise(seedA + 13.1f, seedB + 7.7f, angle01, 3f) - 0.5f) * 2f;
                var mid = (contourMath.SamplePeriodicNoise(seedB + 19.3f, seedC + 5.1f, angle01, 5f) - 0.5f) * 2f;
                var high = (contourMath.SamplePeriodicNoise(seedC + 23.7f, seedA + 3.9f, angle01, 10f) - 0.5f) * 2f;
                var micro = (contourMath.SamplePeriodicNoise(seedD + 29.9f, seedB + 2.7f, angle01, 18f) - 0.5f) * 2f;
                var ultra = (contourMath.SamplePeriodicNoise(seedA + 41.3f, seedD + 6.9f, angle01, 28f) - 0.5f) * 2f;

                var jaggedMid = contourMath.SignedPow(mid, 1.35f);
                var jaggedHigh = contourMath.SignedPow(high, 1.55f);
                var jaggedMicro = contourMath.SignedPow(micro, 1.80f);
                var jaggedUltra = contourMath.SignedPow(ultra, 2.10f);

                var layered = 0f;
                layered += low * 0.24f;
                layered += jaggedMid * 0.28f;
                layered += jaggedHigh * 0.24f;
                layered += jaggedMicro * 0.16f;
                layered += jaggedUltra * 0.08f;

                var finalStrength = baseStrength * sectorMask * roughnessMask;
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i] * (1f + layered * finalStrength), 0.01f);
            }
        }

        private void ClampRadii()
        {
            var minimumRadius = Mathf.Min(context.SemiMajor, context.SemiMinor) * 0.18f;
            for (var i = 0; i < context.OuterRadii.Length; i++)
            {
                context.OuterRadii[i] = Mathf.Max(context.OuterRadii[i], minimumRadius);
            }
        }
    }

    public class IslandContourSupplementaryContoursTask
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourSupplementaryContoursTask(IslandContourGenerationContext context, IslandContourMath contourMath)
        {
            this.context = context;
            this.contourMath = contourMath;
        }

        public void Execute()
        {
            BuildInnerWaterContours();
            BuildOffshoreIslets();
        }

        private void BuildInnerWaterContours()
        {
            if (context.Preset.MassLayoutType != IslandMassLayoutType.Ring)
            {
                return;
            }

            var lagoonScale = Mathf.Lerp(0.42f, 0.58f, 1f - context.Preset.FootprintFill);
            var innerRadii = new float[context.OuterRadii.Length];
            var lagoonSeedX = Mathf.Abs(context.Seed * 0.017f) + 17.3f;
            var lagoonSeedY = Mathf.Abs(context.Seed * 0.029f) + 43.9f;
            for (var i = 0; i < innerRadii.Length; i++)
            {
                var angle01 = i / (float)innerRadii.Length;
                var detail = ((contourMath.SamplePeriodicNoise(lagoonSeedX, lagoonSeedY, angle01, 2f) - 0.5f) * 2f) * 0.06f;
                innerRadii[i] = Mathf.Max(context.OuterRadii[i] * (lagoonScale + detail), Mathf.Min(context.SemiMajor, context.SemiMinor) * 0.08f);
            }

            context.InnerWaterContours.Add(contourMath.RadiiToLoop(innerRadii));
        }

        private void BuildOffshoreIslets()
        {
            if (context.OffshoreIsletCount <= 0)
            {
                return;
            }

            for (var i = 0; i < context.OffshoreIsletCount; i++)
            {
                var angle = contourMath.NextAngle(context.Random);
                var sampleIndex = Mathf.Clamp(Mathf.RoundToInt(angle / contourMath.Tau * context.OuterRadii.Length), 0, context.OuterRadii.Length - 1);
                var distance = context.OuterRadii[sampleIndex] * Mathf.Lerp(1.18f, 1.45f, contourMath.NextFloat(context.Random));
                var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var radius = Mathf.Min(context.SemiMajor, context.SemiMinor) * Mathf.Lerp(0.06f, 0.12f, contourMath.NextFloat(context.Random));
                var isletSeed = unchecked(context.Seed * 397 + i * 997 + sampleIndex * 53);
                context.OffshoreIslets.Add(contourMath.CreateOrganicIsletLoop(center, radius, 24, context.Random, isletSeed, context.Preset, context.FinalCoastlineComplexity));
            }
        }
    }

    public class IslandContourFinalizeTask
    {
        private readonly IslandContourGenerationContext context;
        private readonly IslandContourMath contourMath;

        public IslandContourFinalizeTask(IslandContourGenerationContext context, IslandContourMath contourMath)
        {
            this.context = context;
            this.contourMath = contourMath;
        }

        public void Execute()
        {
            var currentArea = Mathf.Abs(contourMath.ComputePolygonArea(context.OuterLoop));
            var areaScale = currentArea > 0.0001f ? Mathf.Sqrt(context.TargetArea / currentArea) : 1f;

            contourMath.ScaleLoop(context.OuterLoop, areaScale);
            contourMath.ScaleLoops(context.InnerWaterContours, areaScale);
            contourMath.ScaleLoops(context.OffshoreIslets, areaScale);

            contourMath.RotateLoop(context.OuterLoop, context.DirectionDegrees);
            contourMath.RotateLoops(context.InnerWaterContours, context.DirectionDegrees);
            contourMath.RotateLoops(context.OffshoreIslets, context.DirectionDegrees);

            context.ClosedContours.Clear();
            context.ClosedContours.Add(contourMath.CloseLoop(context.OuterLoop));

            for (var i = 0; i < context.InnerWaterContours.Count; i++)
            {
                context.ClosedContours.Add(contourMath.CloseLoop(context.InnerWaterContours[i]));
            }

            for (var i = 0; i < context.OffshoreIslets.Count; i++)
            {
                context.ClosedContours.Add(contourMath.CloseLoop(context.OffshoreIslets[i]));
            }
        }
    }
}