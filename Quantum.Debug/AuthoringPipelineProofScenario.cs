using System.Collections.Generic;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Debug
{
    public sealed class AuthoringPipelineProofScenario
    {
        public const string FixtureName = "authoring-pipeline-proof";
        public const int FrameCount = 9;
        public const int TrainCarCount = 5;

        private const double FrameInterval = 6.0;
        private const double LeadDistance = 36.0;
        private const double CarSpacing = 6.0;
        private const double AxisLength = 4.0;

        private AuthoringPipelineProofScenario(
            TrackAuthoringDefinition definition,
            TrackAuthoringCompilation compilation,
            TrackFrame[] frames,
            TrainPoseResult trainPose,
            TrainPoseExportV1Dto trainPoseExport,
            DebugViewportSnapshotV1Dto snapshot)
        {
            Definition = definition;
            Compilation = compilation;
            Frames = frames;
            TrainPose = trainPose;
            TrainPoseExport = trainPoseExport;
            Snapshot = snapshot;
        }

        public TrackAuthoringDefinition Definition { get; }

        public TrackAuthoringCompilation Compilation { get; }

        public IReadOnlyList<TrackFrame> Frames { get; }

        public TrainPoseResult TrainPose { get; }

        public TrainPoseExportV1Dto TrainPoseExport { get; }

        public DebugViewportSnapshotV1Dto Snapshot { get; }

        public static AuthoringPipelineProofScenario CreateDeterministic()
        {
            var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("authoring-entry", length: 12.0, rollRadians: 0.0),
                new ConstantCurvatureSectionDefinition(
                    "authoring-arc",
                    length: 24.0,
                    radius: 24.0,
                    rollRadians: 0.0),
                new StraightSectionDefinition("authoring-exit", length: 12.0, rollRadians: 0.0)
            });

            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
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

            return new AuthoringPipelineProofScenario(
                definition,
                compilation,
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
