using System.Collections.Generic;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Debug
{
    public sealed class TransitionAuthoringProofScenario
    {
        public const string FixtureName = "transition-authoring-proof";
        public const int FrameCount = 17;
        public const int TrainCarCount = 5;

        private const double FrameInterval = 3.0;
        private const double LeadDistance = 36.0;
        private const double CarSpacing = 6.0;
        private const double AxisLength = 4.0;

        private TransitionAuthoringProofScenario(
            TrackAuthoringDefinition definition,
            TrackAuthoringCompilation compilation,
            TrackAuthoringBoundaryContinuityReport continuity,
            TrackFrame[] frames,
            TrainPoseResult trainPose,
            TrainPoseExportV1Dto trainPoseExport,
            DebugViewportSnapshotV1Dto snapshot)
        {
            Definition = definition;
            Compilation = compilation;
            Continuity = continuity;
            Frames = frames;
            TrainPose = trainPose;
            TrainPoseExport = trainPoseExport;
            Snapshot = snapshot;
        }

        public TrackAuthoringDefinition Definition { get; }

        public TrackAuthoringCompilation Compilation { get; }

        public TrackAuthoringBoundaryContinuityReport Continuity { get; }

        public IReadOnlyList<TrackFrame> Frames { get; }

        public TrainPoseResult TrainPose { get; }

        public TrainPoseExportV1Dto TrainPoseExport { get; }

        public DebugViewportSnapshotV1Dto Snapshot { get; }

        public static TransitionAuthoringProofScenario CreateDeterministic()
        {
            const double ArcCurvature = 1.0 / 20.0;
            var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("transition-entry", length: 12.0, rollRadians: 0.0),
                new CurvatureTransitionSectionDefinition(
                    "transition-in",
                    length: 6.0,
                    startCurvature: 0.0,
                    endCurvature: ArcCurvature,
                    rollRadians: 0.0),
                new ConstantCurvatureSectionDefinition(
                    "transition-arc",
                    length: 12.0,
                    radius: 20.0,
                    rollRadians: 0.0),
                new CurvatureTransitionSectionDefinition(
                    "transition-out",
                    length: 6.0,
                    startCurvature: ArcCurvature,
                    endCurvature: 0.0,
                    rollRadians: 0.0),
                new StraightSectionDefinition("transition-exit", length: 12.0, rollRadians: 0.0)
            });

            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            TrackAuthoringBoundaryContinuityReport continuity =
                TrackAuthoringBoundaryContinuityDiagnostics.Analyze(definition);
            var evaluator = new TrackEvaluator(compilation.Document);
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

            return new TransitionAuthoringProofScenario(
                definition,
                compilation,
                continuity,
                frames,
                trainPose,
                trainPoseExport,
                snapshot);
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
