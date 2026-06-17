using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.IO.TrackLayout.V1
{
    public sealed class TrackLayoutPackageV1ImportResult
    {
        public TrackLayoutPackageV1ImportResult(
            bool success,
            TrackAuthoringDefinition? definition,
            IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            Success = success;
            Definition = definition;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public bool Success { get; }

        public TrackAuthoringDefinition? Definition { get; }

        public IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> Diagnostics { get; }

        internal static TrackLayoutPackageV1ImportResult Failure(
            IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            return new TrackLayoutPackageV1ImportResult(false, null, diagnostics);
        }
    }

    /// <summary>
    /// Maps authored track-layout DTOs to and from backend authoring definitions.
    /// </summary>
    public static class TrackLayoutPackageV1Mapper
    {
        public static TrackLayoutPackageV1ImportResult Import(TrackLayoutPackageV1Dto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics =
                TrackLayoutPackageV1Validator.Validate(dto);
            if (diagnostics.Count != 0)
            {
                return new TrackLayoutPackageV1ImportResult(false, null, diagnostics);
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

                return new TrackLayoutPackageV1ImportResult(
                    true,
                    definition,
                    Array.Empty<TrackLayoutPackageV1ValidationDiagnostic>());
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException)
            {
                return TrackLayoutPackageV1ImportResult.Failure(
                    new[]
                    {
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.MappingFailed,
                            "dto",
                            "Validated TrackLayoutPackageV1 DTO could not be mapped; this likely indicates " +
                            "validator/mapper parity drift. Mapper detail: " + ex.Message)
                    });
            }
        }

        public static TrackLayoutPackageV1Dto Export(TrackAuthoringDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new TrackLayoutPackageV1Dto
            {
                Contract = TrackLayoutPackageV1Dto.ContractName,
                Version = TrackLayoutPackageV1Dto.ContractVersion,
                Metadata = new TrackLayoutMetadataV1Dto(),
                StartPose = MapStartPose(definition.StartPose),
                Sections = MapSections(definition.Sections),
                Banking = definition.Banking == null ? null : MapBanking(definition.Banking)
            };
        }

        private static GeometricSectionDefinition MapSection(TrackLayoutSectionV1Dto section)
        {
            switch (section.Kind)
            {
                case TrackLayoutPackageV1Vocabulary.StraightSectionKind:
                    return new StraightSectionDefinition(
                        section.Id,
                        section.Length,
                        section.RollRadians);

                case TrackLayoutPackageV1Vocabulary.ConstantCurvatureSectionKind:
                    return new ConstantCurvatureSectionDefinition(
                        section.Id,
                        section.Length,
                        section.Radius!.Value,
                        section.RollRadians);

                case TrackLayoutPackageV1Vocabulary.CurvatureTransitionSectionKind:
                    return new CurvatureTransitionSectionDefinition(
                        section.Id,
                        section.Length,
                        section.StartCurvature!.Value,
                        section.EndCurvature!.Value,
                        MapCurvatureInterpolation(section.InterpolationMode),
                        section.RollRadians);

                case TrackLayoutPackageV1Vocabulary.SpatialSectionKind:
                    return new SpatialSectionDefinition(
                        section.Id,
                        section.Length,
                        MapVectors(section.ControlPoints!),
                        section.Degree!.Value,
                        section.Weights!,
                        section.RollRadians);

                default:
                    throw new NotSupportedException(
                        "Unsupported TrackLayoutPackageV1 section kind '" + section.Kind + "'.");
            }
        }

        private static TrackLayoutSectionV1Dto[] MapSections(
            IReadOnlyList<GeometricSectionDefinition> sections)
        {
            var result = new TrackLayoutSectionV1Dto[sections.Count];
            for (int i = 0; i < sections.Count; i++)
            {
                GeometricSectionDefinition section = sections[i];
                if (section is StraightSectionDefinition)
                {
                    result[i] = new TrackLayoutSectionV1Dto
                    {
                        Kind = TrackLayoutPackageV1Vocabulary.StraightSectionKind,
                        Id = section.Id,
                        Length = section.Length,
                        RollRadians = section.RollRadians
                    };
                    continue;
                }

                if (section is ConstantCurvatureSectionDefinition constantCurvature)
                {
                    result[i] = new TrackLayoutSectionV1Dto
                    {
                        Kind = TrackLayoutPackageV1Vocabulary.ConstantCurvatureSectionKind,
                        Id = constantCurvature.Id,
                        Length = constantCurvature.Length,
                        RollRadians = constantCurvature.RollRadians,
                        Radius = constantCurvature.Radius
                    };
                    continue;
                }

                if (section is CurvatureTransitionSectionDefinition transition)
                {
                    result[i] = new TrackLayoutSectionV1Dto
                    {
                        Kind = TrackLayoutPackageV1Vocabulary.CurvatureTransitionSectionKind,
                        Id = transition.Id,
                        Length = transition.Length,
                        RollRadians = transition.RollRadians,
                        StartCurvature = transition.StartCurvature,
                        EndCurvature = transition.EndCurvature,
                        InterpolationMode = MapCurvatureInterpolation(transition.InterpolationMode)
                    };
                    continue;
                }

                if (section is SpatialSectionDefinition spatial)
                {
                    result[i] = new TrackLayoutSectionV1Dto
                    {
                        Kind = TrackLayoutPackageV1Vocabulary.SpatialSectionKind,
                        Id = spatial.Id,
                        Length = spatial.Length,
                        RollRadians = spatial.RollRadians,
                        Degree = spatial.Degree,
                        ControlPoints = MapVectors(spatial.ControlPoints),
                        Weights = MapWeights(spatial.Weights)
                    };
                    continue;
                }

                throw new NotSupportedException(
                    "Unsupported geometric section definition type '" +
                    section.GetType().FullName +
                    "'.");
            }

            return result;
        }

        private static TrackStartPose MapStartPose(TrackStartPoseV1Dto startPose)
        {
            return new TrackStartPose(
                MapVector(startPose.Position),
                MapVector(startPose.Tangent),
                MapVector(startPose.Normal),
                MapVector(startPose.Binormal));
        }

        private static TrackStartPoseV1Dto MapStartPose(TrackStartPose startPose)
        {
            return new TrackStartPoseV1Dto
            {
                Position = MapVector(startPose.Position),
                Tangent = MapVector(startPose.Tangent),
                Normal = MapVector(startPose.Normal),
                Binormal = MapVector(startPose.Binormal)
            };
        }

        private static TrackBankingDefinition? MapBanking(TrackBankingV1Dto? banking)
        {
            if (banking == null)
            {
                return null;
            }

            var keys = new BankingProfileKey[banking.Keys.Length];
            for (int i = 0; i < banking.Keys.Length; i++)
            {
                TrackBankingKeyV1Dto key = banking.Keys[i];
                keys[i] = new BankingProfileKey(
                    key.Distance,
                    key.RollRadians,
                    MapBankingInterpolation(key.InterpolationToNext));
            }

            return new TrackBankingDefinition(keys);
        }

        private static TrackBankingV1Dto MapBanking(TrackBankingDefinition banking)
        {
            IReadOnlyList<BankingProfileKey> sourceKeys = banking.Keys;
            var keys = new TrackBankingKeyV1Dto[sourceKeys.Count];
            for (int i = 0; i < sourceKeys.Count; i++)
            {
                BankingProfileKey sourceKey = sourceKeys[i];
                keys[i] = new TrackBankingKeyV1Dto
                {
                    Distance = sourceKey.Distance,
                    RollRadians = sourceKey.RollRadians,
                    InterpolationToNext = MapBankingInterpolation(sourceKey.InterpolationToNext)
                };
            }

            return new TrackBankingV1Dto
            {
                Keys = keys
            };
        }

        private static CurvatureTransitionInterpolationMode MapCurvatureInterpolation(
            string? interpolationMode)
        {
            switch (interpolationMode)
            {
                case TrackLayoutPackageV1Vocabulary.CurvatureInterpolationLinear:
                    return CurvatureTransitionInterpolationMode.Linear;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationMode),
                        interpolationMode,
                        "Unsupported curvature transition interpolation mode.");
            }
        }

        private static string MapCurvatureInterpolation(
            CurvatureTransitionInterpolationMode interpolationMode)
        {
            switch (interpolationMode)
            {
                case CurvatureTransitionInterpolationMode.Linear:
                    return TrackLayoutPackageV1Vocabulary.CurvatureInterpolationLinear;

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
                case TrackLayoutPackageV1Vocabulary.BankingInterpolationConstant:
                    return BankingProfileInterpolationMode.Constant;

                case TrackLayoutPackageV1Vocabulary.BankingInterpolationLinear:
                    return BankingProfileInterpolationMode.Linear;

                case TrackLayoutPackageV1Vocabulary.BankingInterpolationSmoothStep:
                    return BankingProfileInterpolationMode.SmoothStep;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationToNext),
                        interpolationToNext,
                        "Unsupported banking interpolation mode.");
            }
        }

        private static string MapBankingInterpolation(
            BankingProfileInterpolationMode interpolationToNext)
        {
            switch (interpolationToNext)
            {
                case BankingProfileInterpolationMode.Constant:
                    return TrackLayoutPackageV1Vocabulary.BankingInterpolationConstant;

                case BankingProfileInterpolationMode.Linear:
                    return TrackLayoutPackageV1Vocabulary.BankingInterpolationLinear;

                case BankingProfileInterpolationMode.SmoothStep:
                    return TrackLayoutPackageV1Vocabulary.BankingInterpolationSmoothStep;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationToNext),
                        interpolationToNext,
                        "Unsupported banking interpolation mode.");
            }
        }

        private static Vector3d[] MapVectors(TrackLayoutVector3dV1Dto[] vectors)
        {
            var result = new Vector3d[vectors.Length];
            for (int i = 0; i < vectors.Length; i++)
            {
                result[i] = MapVector(vectors[i]);
            }

            return result;
        }

        private static TrackLayoutVector3dV1Dto[] MapVectors(IReadOnlyList<Vector3d> vectors)
        {
            var result = new TrackLayoutVector3dV1Dto[vectors.Count];
            for (int i = 0; i < vectors.Count; i++)
            {
                result[i] = MapVector(vectors[i]);
            }

            return result;
        }

        private static Vector3d MapVector(TrackLayoutVector3dV1Dto vector)
        {
            return new Vector3d(vector.X, vector.Y, vector.Z);
        }

        private static TrackLayoutVector3dV1Dto MapVector(Vector3d vector)
        {
            return new TrackLayoutVector3dV1Dto
            {
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }

        private static double[] MapWeights(IReadOnlyList<double> weights)
        {
            var result = new double[weights.Count];
            for (int i = 0; i < weights.Count; i++)
            {
                result[i] = weights[i];
            }

            return result;
        }
    }
}
