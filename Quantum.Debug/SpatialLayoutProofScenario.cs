using System.Collections.Generic;
using System.Linq;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;
using TrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Debug
{
    public sealed class SpatialLayoutProofScenario
    {
        public const string FixtureName = "spatial-layout-proof";
        public const int FrameCount = 25;
        public const int TrainCarCount = 9;

        private const double SpatialSectionLength = 18.0;
        private const double FrameInterval = 3.0;
        private const double LeadDistance = 60.0;
        private const double CarSpacing = 6.0;
        private const double AxisLength = 4.0;
        private const double RuntimeLengthHeadroom = 1e-9;
        private const int SpatialDegree = 3;
        private const int SpatialLengthNormalizationIterations = 3;

        private SpatialLayoutProofScenario(
            TrackAuthoringDefinition definition,
            TrackAuthoringCompilation compilation,
            TrackAuthoringGeometryContinuityReport geometryContinuity,
            TrackFrame[] frames,
            TrainPoseResult trainPose,
            TrainPoseExportV1Dto trainPoseExport,
            DebugViewportSnapshotV1Dto snapshot)
        {
            Definition = definition;
            Compilation = compilation;
            GeometryContinuity = geometryContinuity;
            Frames = frames;
            TrainPose = trainPose;
            TrainPoseExport = trainPoseExport;
            Snapshot = snapshot;
        }

        public TrackAuthoringDefinition Definition { get; }

        public TrackAuthoringCompilation Compilation { get; }

        public TrackAuthoringGeometryContinuityReport GeometryContinuity { get; }

        public TrackAuthoringGeometryContinuityReport Continuity => GeometryContinuity;

        public IReadOnlyList<TrackFrame> Frames { get; }

        public TrainPoseResult TrainPose { get; }

        public TrainPoseExportV1Dto TrainPoseExport { get; }

        public DebugViewportSnapshotV1Dto Snapshot { get; }

        public static SpatialLayoutProofScenario CreateDeterministic()
        {
            TrackStartPose startPose = CreateStartPose();
            var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("spatial-layout-entry", length: 12.0, rollRadians: 0.0),
                CreateSpatialSection(
                    "spatial-layout-rise-turn",
                    new[]
                    {
                        Vector3d.Zero,
                        new Vector3d(2.0, 0.0, 0.0),
                        new Vector3d(4.0, 0.0, 0.0),
                        new Vector3d(7.0, 1.5, 1.0),
                        new Vector3d(9.0, 3.5, 3.5),
                        new Vector3d(11.0, 4.0, 6.0),
                        new Vector3d(13.0, 4.0, 8.0),
                        new Vector3d(15.0, 4.0, 10.0)
                    }),
                new StraightSectionDefinition("spatial-layout-elevated", length: 12.0, rollRadians: 0.0),
                CreateSpatialSection(
                    "spatial-layout-descend-counter-turn",
                    new[]
                    {
                        Vector3d.Zero,
                        new Vector3d(2.0, 0.0, 0.0),
                        new Vector3d(4.0, 0.0, 0.0),
                        new Vector3d(7.0, -2.0, -1.0),
                        new Vector3d(9.0, -4.5, -3.5),
                        new Vector3d(11.0, -6.0, -6.0),
                        new Vector3d(13.0, -6.7, -8.0),
                        new Vector3d(15.0, -7.4, -10.0)
                    }),
                new StraightSectionDefinition("spatial-layout-exit", length: 12.0, rollRadians: 0.0)
            }, startPose);

            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            TrackAuthoringGeometryContinuityReport geometryContinuity =
                TrackAuthoringGeometryContinuityDiagnostics.Analyze(compilation);
            var evaluator = new TrackEvaluator(compilation.Runtime);
            TrackFrame[] frames = evaluator.EvaluateFramesAtDistances(BuildFrameDistances());

            var trainDefinition = new TrainConsistDefinition(
                carCount: TrainCarCount,
                carSpacing: CarSpacing,
                carLength: 5.0,
                carWidth: 1.8,
                carHeight: 2.2,
                bogieSpacing: 4.0,
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 2,
                    wheelRadius: 0.45,
                    wheelWidth: 0.25,
                    axleSpacing: 1.1));
            var provider = new TrainCarTransformProvider(evaluator);
            TrainPoseResult trainPose = provider.EvaluateTrainPose(LeadDistance, trainDefinition);
            TrainPoseExportV1Dto trainPoseExport = TrainPoseExportV1Mapper.Export(trainPose);

            var source = new DebugViewportSnapshotV1Source
            {
                Units = "meters",
                SourceFixtureName = FixtureName,
                SampledFrames = frames,
                Lines = TrackFrameDebugGizmoBuilder.BuildAxes(frames[frames.Length / 2], AxisLength),
                Boxes = BuildTrainBodyBoxes(trainPose),
                TrainPose = trainPose
            };
            DebugViewportSnapshotV1Dto snapshot = DebugViewportSnapshotV1Mapper.Export(source);

            return new SpatialLayoutProofScenario(
                definition,
                compilation,
                geometryContinuity,
                frames,
                trainPose,
                trainPoseExport,
                snapshot);
        }

        private static TrackStartPose CreateStartPose()
        {
            double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);
            return new TrackStartPose(
                new Vector3d(20.0, 4.0, -10.0),
                new Vector3d(inverseSqrtTwo, 0.0, inverseSqrtTwo),
                Vector3d.UnitY,
                new Vector3d(-inverseSqrtTwo, 0.0, inverseSqrtTwo));
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

        private static double[] BuildFrameDistances()
        {
            var distances = new double[FrameCount];

            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = i * FrameInterval;
            }

            return distances;
        }

        private static DebugViewportBoxSource[] BuildTrainBodyBoxes(TrainPoseResult trainPose)
        {
            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = trainPose.CarsReadOnly;
            TrainCarGeometry geometry = trainPose.Definition.CarGeometry;
            var boxes = new DebugViewportBoxSource[cars.Count];

            for (int i = 0; i < cars.Count; i++)
            {
                boxes[i] = new DebugViewportBoxSource(
                    role: DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                    label: "car-" + i,
                    frame: cars[i].Body.ArticulatedFrame,
                    length: geometry.Length,
                    width: geometry.Width,
                    height: geometry.Height);
            }

            return boxes;
        }
    }
}
