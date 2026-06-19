using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.IO.TrackLayout.V2
{
    public sealed class TrackLayoutPackageV2ImportResult
    {
        public TrackLayoutPackageV2ImportResult(
            bool success,
            TrackAuthoringDefinition? definition,
            HeartlineOffset? heartlineOffset,
            IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            Success = success;
            Definition = definition;
            HeartlineOffset = heartlineOffset;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public bool Success { get; }

        public TrackAuthoringDefinition? Definition { get; }

        public HeartlineOffset? HeartlineOffset { get; }

        public IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> Diagnostics { get; }

        internal static TrackLayoutPackageV2ImportResult Failure(
            IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            return new TrackLayoutPackageV2ImportResult(false, null, null, diagnostics);
        }
    }

    /// <summary>
    /// Maps TrackLayoutPackageV2 DTOs into backend authoring definitions.
    /// </summary>
    public static class TrackLayoutPackageV2Mapper
    {
        public static TrackLayoutPackageV2ImportResult Import(TrackLayoutPackageV2Dto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics =
                TrackLayoutPackageV2Validator.Validate(dto);
            if (diagnostics.Count != 0)
            {
                return TrackLayoutPackageV2ImportResult.Failure(diagnostics);
            }

            try
            {
                var sections = new List<GeometricSectionDefinition>(dto.Sections.Length);
                for (int i = 0; i < dto.Sections.Length; i++)
                {
                    sections.Add(MapSection(dto.Sections[i]));
                }

                TrackStartPose startPose = MapStartPose(dto.StartPose);
                TrackBankingDefinition? banking = MapBanking(dto.Banking);
                TrackAuthoringDefinition definition = banking == null
                    ? new TrackAuthoringDefinition(sections, startPose)
                    : new TrackAuthoringDefinition(sections, startPose, banking);
                HeartlineOffset? heartlineOffset = MapHeartline(dto.Heartline);

                return new TrackLayoutPackageV2ImportResult(
                    true,
                    definition,
                    heartlineOffset,
                    Array.Empty<TrackLayoutPackageV2ValidationDiagnostic>());
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException)
            {
                return TrackLayoutPackageV2ImportResult.Failure(
                    new[]
                    {
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.MappingFailed,
                            "dto",
                            "Validated TrackLayoutPackageV2 DTO could not be mapped; this likely indicates " +
                            "validator/mapper parity drift. Mapper detail: " + ex.Message)
                    });
            }
        }

        private static GeometricSectionDefinition MapSection(TrackLayoutSectionV2Dto section)
        {
            switch (section.Kind)
            {
                case TrackLayoutPackageV2Vocabulary.StraightSectionKind:
                    return new StraightSectionDefinition(
                        section.Id,
                        section.Length,
                        section.RollRadians);

                case TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind:
                    return new ConstantCurvatureSectionDefinition(
                        section.Id,
                        section.Length,
                        section.Radius!.Value,
                        section.RollRadians);

                case TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind:
                    return new CurvatureTransitionSectionDefinition(
                        section.Id,
                        section.Length,
                        section.StartCurvature!.Value,
                        section.EndCurvature!.Value,
                        MapCurvatureInterpolation(section.InterpolationMode),
                        section.RollRadians);

                case TrackLayoutPackageV2Vocabulary.SpatialSectionKind:
                    return new SpatialSectionDefinition(
                        section.Id,
                        section.Length,
                        MapVectors(section.ControlPoints!),
                        section.Degree!.Value,
                        section.Weights!,
                        section.RollRadians);

                default:
                    throw new NotSupportedException(
                        "Unsupported TrackLayoutPackageV2 section kind '" + section.Kind + "'.");
            }
        }

        private static TrackStartPose MapStartPose(TrackStartPoseV2Dto startPose)
        {
            return new TrackStartPose(
                MapVector(startPose.Position),
                MapVector(startPose.Tangent),
                MapVector(startPose.Normal),
                MapVector(startPose.Binormal));
        }

        private static TrackBankingDefinition? MapBanking(TrackBankingV2Dto? banking)
        {
            if (banking == null)
            {
                return null;
            }

            var keys = new BankingProfileKey[banking.Keys.Length];
            for (int i = 0; i < banking.Keys.Length; i++)
            {
                TrackBankingKeyV2Dto key = banking.Keys[i];
                keys[i] = new BankingProfileKey(
                    key.Distance,
                    key.RollRadians,
                    MapBankingInterpolation(key.InterpolationToNext));
            }

            return new TrackBankingDefinition(keys);
        }

        private static HeartlineOffset? MapHeartline(TrackHeartlineV2Dto? heartline)
        {
            if (heartline == null)
            {
                return null;
            }

            if (!string.Equals(
                    heartline.Kind,
                    TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    heartline.DistanceDomain,
                    TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    heartline.AxisSource,
                    TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame,
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    "Only constantOffset heartline offsets over centerlineStation using sampledFrame axes are supported.");
            }

            return new HeartlineOffset(heartline.NormalOffset, heartline.LateralOffset);
        }

        private static CurvatureTransitionInterpolationMode MapCurvatureInterpolation(
            string? interpolationMode)
        {
            switch (interpolationMode)
            {
                case TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear:
                    return CurvatureTransitionInterpolationMode.Linear;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationMode),
                        interpolationMode,
                        "Unsupported curvature transition interpolation mode.");
            }
        }

        private static BankingProfileInterpolationMode MapBankingInterpolation(
            string interpolationToNext)
        {
            switch (interpolationToNext)
            {
                case TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant:
                    return BankingProfileInterpolationMode.Constant;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationLinear:
                    return BankingProfileInterpolationMode.Linear;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep:
                    return BankingProfileInterpolationMode.SmoothStep;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationQuadratic:
                    return BankingProfileInterpolationMode.Quadratic;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationCubic:
                    return BankingProfileInterpolationMode.Cubic;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationQuartic:
                    return BankingProfileInterpolationMode.Quartic;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationQuintic:
                    return BankingProfileInterpolationMode.Quintic;

                case TrackLayoutPackageV2Vocabulary.BankingInterpolationSinusoidal:
                    return BankingProfileInterpolationMode.Sinusoidal;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationToNext),
                        interpolationToNext,
                        "Unsupported banking interpolation mode.");
            }
        }

        private static Vector3d[] MapVectors(TrackLayoutVector3dV2Dto[] vectors)
        {
            var result = new Vector3d[vectors.Length];
            for (int i = 0; i < vectors.Length; i++)
            {
                result[i] = MapVector(vectors[i]);
            }

            return result;
        }

        private static Vector3d MapVector(TrackLayoutVector3dV2Dto vector)
        {
            return new Vector3d(vector.X, vector.Y, vector.Z);
        }
    }
}
