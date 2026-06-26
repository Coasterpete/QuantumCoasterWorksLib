using System.Collections.Generic;
using System.Linq;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Debug
{
    public sealed class ProfileBankedHeartlineProofScenario
    {
        public const string FixtureName = "profile-banked-heartline-proof";
        public const int SampleCount = 15;
        public const double SampleInterval = 4.0;
        public const double TotalLength = 56.0;

        private const double EntryLength = 8.0;
        private const double SpatialSectionLength = 16.0;
        private const double ElevatedHoldLength = 8.0;
        private const double ExitLength = 8.0;
        private const double RuntimeLengthHeadroom = 1e-9;
        private const int SpatialDegree = 3;
        private const int SpatialLengthNormalizationIterations = 3;

        private ProfileBankedHeartlineProofScenario(
            TrackBankingDefinition authoredBanking,
            TrackAuthoringDefinition definition,
            TrackAuthoringCompilation compilation,
            TrackAuthoringGeometryContinuityReport geometryContinuity,
            TrackAuthoringBankingDiagnosticsReport bankingDiagnostics,
            HeartlineOffset heartlineOffset,
            double[] sampleDistances,
            TrackFrame[] defaultCenterlineFrames,
            TrackFrame[] profileBankedFrames,
            HeartlineFrame[] defaultHeartlineFrames,
            HeartlineFrame[] profileBankedHeartlineFrames)
        {
            AuthoredBanking = authoredBanking;
            Definition = definition;
            Compilation = compilation;
            GeometryContinuity = geometryContinuity;
            BankingDiagnostics = bankingDiagnostics;
            HeartlineOffset = heartlineOffset;
            SampleDistances = sampleDistances;
            DefaultCenterlineFrames = defaultCenterlineFrames;
            ProfileBankedFrames = profileBankedFrames;
            DefaultHeartlineFrames = defaultHeartlineFrames;
            ProfileBankedHeartlineFrames = profileBankedHeartlineFrames;
        }

        public TrackBankingDefinition AuthoredBanking { get; }

        public TrackAuthoringDefinition Definition { get; }

        public TrackAuthoringCompilation Compilation { get; }

        public TrackAuthoringGeometryContinuityReport GeometryContinuity { get; }

        public TrackAuthoringBankingDiagnosticsReport BankingDiagnostics { get; }

        public HeartlineOffset HeartlineOffset { get; }

        public IReadOnlyList<double> SampleDistances { get; }

        public IReadOnlyList<TrackFrame> DefaultCenterlineFrames { get; }

        public IReadOnlyList<TrackFrame> ProfileBankedFrames { get; }

        public IReadOnlyList<HeartlineFrame> DefaultHeartlineFrames { get; }

        public IReadOnlyList<HeartlineFrame> ProfileBankedHeartlineFrames { get; }

        public static ProfileBankedHeartlineProofScenario CreateDeterministic()
        {
            TrackBankingDefinition authoredBanking = CreateAuthoredBanking();
            var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition(
                    "profile-banked-heartline-entry",
                    EntryLength,
                    rollRadians: 0.0),
                CreateSpatialSection(
                    "profile-banked-heartline-rise-turn",
                    new[]
                    {
                        Vector3d.Zero,
                        new Vector3d(2.0, 0.0, 0.0),
                        new Vector3d(4.0, 0.0, 0.0),
                        new Vector3d(6.5, 1.0, 0.5),
                        new Vector3d(8.5, 3.0, 2.0),
                        new Vector3d(10.5, 4.8, 4.0),
                        new Vector3d(12.5, 4.8, 5.5),
                        new Vector3d(14.5, 4.8, 7.0)
                    }),
                new StraightSectionDefinition(
                    "profile-banked-heartline-elevated-hold",
                    ElevatedHoldLength,
                    rollRadians: 0.0),
                CreateSpatialSection(
                    "profile-banked-heartline-descend-counterturn",
                    new[]
                    {
                        Vector3d.Zero,
                        new Vector3d(2.0, 0.0, 0.0),
                        new Vector3d(4.0, 0.0, 0.0),
                        new Vector3d(6.5, -0.7, -0.8),
                        new Vector3d(8.5, -2.6, -2.5),
                        new Vector3d(10.5, -4.8, -4.2),
                        new Vector3d(12.5, -4.8, -5.7),
                        new Vector3d(14.5, -4.8, -7.2)
                    }),
                new StraightSectionDefinition(
                    "profile-banked-heartline-exit",
                    ExitLength,
                    rollRadians: 0.0)
            }, TrackStartPose.Identity, authoredBanking);

            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            TrackAuthoringGeometryContinuityReport geometryContinuity =
                TrackAuthoringGeometryContinuityDiagnostics.Analyze(compilation);
            TrackAuthoringBankingDiagnosticsReport bankingDiagnostics =
                TrackAuthoringBankingDiagnostics.Analyze(compilation);
            var evaluator = new TrackEvaluator(compilation.Runtime);
            double[] sampleDistances = BuildSampleDistances();
            TrackFrame[] defaultCenterlineFrames = evaluator.EvaluateFramesAtDistances(sampleDistances);
            TrackFrame[] profileBankedFrames = BankingProfileSampler.SampleFramesAtDistances(
                evaluator,
                compilation.BankingProfile,
                sampleDistances);
            var heartlineOffset = new HeartlineOffset(
                normalOffsetMeters: 1.2,
                lateralOffsetMeters: 0.0);
            HeartlineFrame[] defaultHeartlineFrames = HeartlineSampler.SampleAtDistances(
                evaluator,
                heartlineOffset,
                sampleDistances);
            HeartlineFrame[] profileBankedHeartlineFrames = HeartlineSampler.SampleAtDistances(
                evaluator,
                compilation.BankingProfile,
                heartlineOffset,
                sampleDistances);

            return new ProfileBankedHeartlineProofScenario(
                authoredBanking,
                definition,
                compilation,
                geometryContinuity,
                bankingDiagnostics,
                heartlineOffset,
                sampleDistances,
                defaultCenterlineFrames,
                profileBankedFrames,
                defaultHeartlineFrames,
                profileBankedHeartlineFrames);
        }

        private static TrackBankingDefinition CreateAuthoredBanking()
        {
            return new TrackBankingDefinition(new[]
            {
                new BankingProfileKey(
                    0.0,
                    ToRadians(0.0),
                    BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(
                    8.0,
                    ToRadians(15.0),
                    BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(
                    24.0,
                    ToRadians(30.0),
                    BankingProfileInterpolationMode.Constant),
                new BankingProfileKey(
                    32.0,
                    ToRadians(30.0),
                    BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(
                    48.0,
                    ToRadians(-20.0),
                    BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(
                    56.0,
                    ToRadians(0.0),
                    BankingProfileInterpolationMode.Constant)
            });
        }

        private static SpatialSectionDefinition CreateSpatialSection(
            string id,
            IReadOnlyList<Vector3d> unscaledControlPoints)
        {
            List<Vector3d> controlPoints = unscaledControlPoints.ToList();

            for (int iteration = 0; iteration < SpatialLengthNormalizationIterations; iteration++)
            {
                double measuredLength = MeasureLength(controlPoints);
                double scale = (SpatialSectionLength + RuntimeLengthHeadroom) / measuredLength;
                controlPoints = controlPoints.Select(point => point * scale).ToList();
            }

            var weights = Enumerable.Repeat(1.0, controlPoints.Count).ToList();

            return new SpatialSectionDefinition(
                id,
                SpatialSectionLength,
                controlPoints,
                SpatialDegree,
                weights,
                rollRadians: 0.0);
        }

        private static double MeasureLength(IReadOnlyList<Vector3d> controlPoints)
        {
            var points = controlPoints.ToList();
            var weights = Enumerable.Repeat(1.0, points.Count).ToList();
            var curve = new GSharkNurbsCurveAdapter(points, weights, SpatialDegree);
            TrackSamplingOptions samplingOptions = TrackSamplingOptions.Default;
            return new ArcLengthLUT(
                curve,
                samplingOptions.ArcLengthSamples,
                samplingOptions.ArcLengthTolerance).TotalLength;
        }

        private static double[] BuildSampleDistances()
        {
            var distances = new double[SampleCount];

            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = i * SampleInterval;
            }

            return distances;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * System.Math.PI / 180.0;
        }
    }
}
