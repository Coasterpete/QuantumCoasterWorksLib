using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.IO.TrackLayout.V2
{
    /// <summary>
    /// Immutable non-graph document values preserved beside an imported authoring graph.
    /// </summary>
    /// <remarks>
    /// This is an in-memory compatibility value, not a new public file format. Section
    /// order, start pose, and banking belong to <see cref="TrackAuthoringGraph"/>.
    /// </remarks>
    public sealed class TrackLayoutPackageV2GraphAncillaryState
    {
        public TrackLayoutPackageV2GraphAncillaryState(
            string contract,
            int version,
            string units,
            string? sourceName,
            string? layoutId,
            HeartlineOffset? heartlineOffset)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            Version = version;
            Units = units ?? throw new ArgumentNullException(nameof(units));
            SourceName = sourceName;
            LayoutId = layoutId;
            HeartlineOffset = heartlineOffset;
        }

        public string Contract { get; }

        public int Version { get; }

        public string Units { get; }

        public string? SourceName { get; }

        public string? LayoutId { get; }

        public HeartlineOffset? HeartlineOffset { get; }
    }

    public sealed class TrackLayoutPackageV2GraphImportResult
    {
        private readonly IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> _diagnostics;

        internal TrackLayoutPackageV2GraphImportResult(
            TrackAuthoringGraph? graph,
            TrackLayoutPackageV2GraphAncillaryState? ancillaryState,
            IEnumerable<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            Graph = graph;
            AncillaryState = ancillaryState;
            _diagnostics = new List<TrackLayoutPackageV2ValidationDiagnostic>(
                diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).AsReadOnly();
        }

        public bool Success => Graph != null && AncillaryState != null && _diagnostics.Count == 0;

        public TrackAuthoringGraph? Graph { get; }

        public TrackLayoutPackageV2GraphAncillaryState? AncillaryState { get; }

        public IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class TrackLayoutPackageV2GraphExportResult
    {
        private readonly IReadOnlyList<TrackAuthoringGraphDiagnostic> _graphDiagnostics;
        private readonly IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> _packageDiagnostics;

        internal TrackLayoutPackageV2GraphExportResult(
            TrackLayoutPackageV2Dto? package,
            IEnumerable<TrackAuthoringGraphDiagnostic> graphDiagnostics,
            IEnumerable<TrackLayoutPackageV2ValidationDiagnostic> packageDiagnostics)
        {
            Package = package;
            _graphDiagnostics = new List<TrackAuthoringGraphDiagnostic>(
                graphDiagnostics ?? throw new ArgumentNullException(nameof(graphDiagnostics))).AsReadOnly();
            _packageDiagnostics = new List<TrackLayoutPackageV2ValidationDiagnostic>(
                packageDiagnostics ?? throw new ArgumentNullException(nameof(packageDiagnostics))).AsReadOnly();
        }

        public bool Success =>
            Package != null && _graphDiagnostics.Count == 0 && _packageDiagnostics.Count == 0;

        public TrackLayoutPackageV2Dto? Package { get; }

        public IReadOnlyList<TrackAuthoringGraphDiagnostic> GraphDiagnostics => _graphDiagnostics;

        public IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> PackageDiagnostics =>
            _packageDiagnostics;
    }

    /// <summary>
    /// Compatibility boundary between ordered Track Layout Package V2 persistence and
    /// the editor-facing authoring graph.
    /// </summary>
    public static class TrackLayoutPackageV2GraphAdapter
    {
        public static TrackLayoutPackageV2GraphImportResult Import(TrackLayoutPackageV2Dto dto)
        {
            if (dto is null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            TrackLayoutPackageV2ImportResult import = TrackLayoutPackageV2Mapper.Import(dto);
            if (!import.Success || import.Definition is null)
            {
                return new TrackLayoutPackageV2GraphImportResult(
                    null,
                    null,
                    import.Diagnostics);
            }

            TrackAuthoringDefinition definition = import.Definition;
            var nodes = new TrackAuthoringGraphNode[definition.Sections.Count];
            var edges = new TrackAuthoringGraphEdge[System.Math.Max(0, nodes.Length - 1)];
            for (int i = 0; i < definition.Sections.Count; i++)
            {
                nodes[i] = new TrackAuthoringGraphNode(definition.Sections[i]);
                if (i > 0)
                {
                    edges[i - 1] = new TrackAuthoringGraphEdge(nodes[i - 1].Id, nodes[i].Id);
                }
            }

            var graph = new TrackAuthoringGraph(
                nodes,
                edges,
                definition.StartPose,
                definition.Banking);
            var ancillaryState = new TrackLayoutPackageV2GraphAncillaryState(
                dto.Contract,
                dto.Version,
                dto.Metadata.Units,
                dto.Metadata.SourceName,
                dto.Metadata.LayoutId,
                import.HeartlineOffset);

            return new TrackLayoutPackageV2GraphImportResult(
                graph,
                ancillaryState,
                Array.Empty<TrackLayoutPackageV2ValidationDiagnostic>());
        }

        public static TrackLayoutPackageV2GraphExportResult Export(
            TrackAuthoringGraph graph,
            TrackLayoutPackageV2GraphAncillaryState ancillaryState)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (ancillaryState is null)
            {
                throw new ArgumentNullException(nameof(ancillaryState));
            }

            TrackAuthoringGraphCompileResult graphCompilation =
                TrackAuthoringGraphCompiler.Compile(graph);
            if (!graphCompilation.Success)
            {
                return new TrackLayoutPackageV2GraphExportResult(
                    null,
                    graphCompilation.Diagnostics,
                    Array.Empty<TrackLayoutPackageV2ValidationDiagnostic>());
            }

            var sections = new TrackLayoutSectionV2Dto[graphCompilation.OrderedNodes.Count];
            for (int i = 0; i < graphCompilation.OrderedNodes.Count; i++)
            {
                sections[i] = MapSection(graphCompilation.OrderedNodes[i].Section);
            }

            var dto = new TrackLayoutPackageV2Dto
            {
                Contract = ancillaryState.Contract,
                Version = ancillaryState.Version,
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = ancillaryState.Units,
                    SourceName = ancillaryState.SourceName,
                    LayoutId = ancillaryState.LayoutId
                },
                StartPose = MapStartPose(graph.StartPose),
                Sections = sections,
                Banking = MapBanking(graph.Banking),
                Heartline = MapHeartline(ancillaryState.HeartlineOffset)
            };

            IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> packageDiagnostics =
                TrackLayoutPackageV2Validator.Validate(dto);
            return packageDiagnostics.Count == 0
                ? new TrackLayoutPackageV2GraphExportResult(
                    dto,
                    Array.Empty<TrackAuthoringGraphDiagnostic>(),
                    Array.Empty<TrackLayoutPackageV2ValidationDiagnostic>())
                : new TrackLayoutPackageV2GraphExportResult(
                    null,
                    Array.Empty<TrackAuthoringGraphDiagnostic>(),
                    packageDiagnostics);
        }

        private static TrackLayoutSectionV2Dto MapSection(GeometricSectionDefinition section)
        {
            var dto = new TrackLayoutSectionV2Dto
            {
                Id = section.Id,
                Length = section.Length,
                RollRadians = section.RollRadians
            };

            if (section is StraightSectionDefinition)
            {
                dto.Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind;
                return dto;
            }

            if (section is ConstantCurvatureSectionDefinition arc)
            {
                dto.Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind;
                dto.Radius = arc.Radius;
                return dto;
            }

            if (section is CurvatureTransitionSectionDefinition transition)
            {
                dto.Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind;
                dto.StartCurvature = transition.StartCurvature;
                dto.EndCurvature = transition.EndCurvature;
                dto.InterpolationMode = MapCurvatureInterpolation(transition.InterpolationMode);
                return dto;
            }

            if (section is SpatialSectionDefinition spatial)
            {
                dto.Kind = TrackLayoutPackageV2Vocabulary.SpatialSectionKind;
                dto.Degree = spatial.Degree;
                dto.ControlPoints = MapVectors(spatial.ControlPoints);
                dto.Weights = CopyDoubles(spatial.Weights);
                return dto;
            }

            throw new NotSupportedException(
                $"Unsupported graph section definition type '{section.GetType().FullName}'.");
        }

        private static TrackStartPoseV2Dto MapStartPose(TrackStartPose startPose)
        {
            return new TrackStartPoseV2Dto
            {
                Position = MapVector(startPose.Position),
                Tangent = MapVector(startPose.Tangent),
                Normal = MapVector(startPose.Normal),
                Binormal = MapVector(startPose.Binormal)
            };
        }

        private static TrackBankingV2Dto? MapBanking(TrackBankingDefinition? banking)
        {
            if (banking is null)
            {
                return null;
            }

            var keys = new TrackBankingKeyV2Dto[banking.Keys.Count];
            for (int i = 0; i < banking.Keys.Count; i++)
            {
                BankingProfileKey key = banking.Keys[i];
                keys[i] = new TrackBankingKeyV2Dto
                {
                    Distance = key.Distance,
                    RollRadians = key.RollRadians,
                    InterpolationToNext = MapBankingInterpolation(key.InterpolationToNext)
                };
            }

            return new TrackBankingV2Dto { Keys = keys };
        }

        private static TrackHeartlineV2Dto? MapHeartline(HeartlineOffset? heartlineOffset)
        {
            if (!heartlineOffset.HasValue)
            {
                return null;
            }

            HeartlineOffset offset = heartlineOffset.Value;
            return new TrackHeartlineV2Dto
            {
                Kind = TrackLayoutPackageV2Vocabulary.HeartlineKindConstantOffset,
                DistanceDomain = TrackLayoutPackageV2Vocabulary.HeartlineDistanceDomainCenterlineStation,
                AxisSource = TrackLayoutPackageV2Vocabulary.HeartlineAxisSourceSampledFrame,
                NormalOffset = offset.NormalOffsetMeters,
                LateralOffset = offset.LateralOffsetMeters
            };
        }

        private static string MapCurvatureInterpolation(
            CurvatureTransitionInterpolationMode interpolationMode)
        {
            if (interpolationMode == CurvatureTransitionInterpolationMode.Linear)
            {
                return TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear;
            }

            throw new ArgumentOutOfRangeException(
                nameof(interpolationMode),
                interpolationMode,
                "Unsupported curvature transition interpolation mode.");
        }

        private static string MapBankingInterpolation(BankingProfileInterpolationMode interpolationMode)
        {
            switch (interpolationMode)
            {
                case BankingProfileInterpolationMode.Constant:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant;
                case BankingProfileInterpolationMode.Linear:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationLinear;
                case BankingProfileInterpolationMode.SmoothStep:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep;
                case BankingProfileInterpolationMode.Quadratic:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationQuadratic;
                case BankingProfileInterpolationMode.Cubic:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationCubic;
                case BankingProfileInterpolationMode.Quartic:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationQuartic;
                case BankingProfileInterpolationMode.Quintic:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationQuintic;
                case BankingProfileInterpolationMode.Sinusoidal:
                    return TrackLayoutPackageV2Vocabulary.BankingInterpolationSinusoidal;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(interpolationMode),
                        interpolationMode,
                        "Unsupported banking interpolation mode.");
            }
        }

        private static TrackLayoutVector3dV2Dto[] MapVectors(IReadOnlyList<Vector3d> vectors)
        {
            var result = new TrackLayoutVector3dV2Dto[vectors.Count];
            for (int i = 0; i < vectors.Count; i++)
            {
                result[i] = MapVector(vectors[i]);
            }

            return result;
        }

        private static TrackLayoutVector3dV2Dto MapVector(Vector3d vector)
        {
            return new TrackLayoutVector3dV2Dto
            {
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }

        private static double[] CopyDoubles(IReadOnlyList<double> values)
        {
            var result = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                result[i] = values[i];
            }

            return result;
        }
    }
}
