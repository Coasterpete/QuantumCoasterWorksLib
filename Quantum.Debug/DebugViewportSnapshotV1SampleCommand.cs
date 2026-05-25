using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Quantum.IO.DebugViewport.V1;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class DebugViewportSnapshotV1SampleCommand
    {
        public const int CenterlineSampleCount = 9;
        public const int TrainCarCount = 2;

        internal const string DefaultRelativeOutputPath = "artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json";

        private const string SourceFixtureName = "sampling-perf-smoke";
        private const int ContinuousControlPointSampleCount = 8;
        private const int ContinuousArcLengthSampleCount = 64;
        private const double RollBlendDistance = 6.0;
        private const double AxisLength = 4.0;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            DebugViewportSnapshotV1Dto dto = BuildSample();
            string json = DebugViewportSnapshotV1Json.Serialize(dto, indented: true);

            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote DebugViewportSnapshotV1 sample to '{resolvedOutputPath}'.");
            return 0;
        }

        public static DebugViewportSnapshotV1Dto BuildSample()
        {
            SamplingPerfSmokeScenario scenario = SamplingPerfSmokeScenario.CreateDeterministic();
            double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                scenario.Document.TotalLength,
                CenterlineSampleCount);

            TrackFrame[] frames = DebugTrackContinuousSampler.SampleContinuousFrames(
                scenario.Document,
                scenario.Evaluator,
                distances,
                controlPointSampleCount: ContinuousControlPointSampleCount,
                arcLengthSampleCount: ContinuousArcLengthSampleCount,
                rollBlendDistance: RollBlendDistance);

            TrainConsistDefinition trainDefinition = BuildSampleTrainDefinition(scenario);
            TrainPoseResult trainPose = scenario.Provider.EvaluateTrainPose(
                scenario.LeadDistance,
                trainDefinition);

            DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(
                frames[frames.Length / 2],
                AxisLength);

            var source = new DebugViewportSnapshotV1Source
            {
                Units = "meters",
                SourceFixtureName = SourceFixtureName,
                SampledFrames = frames,
                Lines = lines,
                Boxes = BuildTrainBodyBoxes(trainPose),
                TrainPose = trainPose
            };

            return DebugViewportSnapshotV1Mapper.Export(source);
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static TrainConsistDefinition BuildSampleTrainDefinition(SamplingPerfSmokeScenario scenario)
        {
            return new TrainConsistDefinition(
                carCount: TrainCarCount,
                carSpacing: scenario.CarSpacing,
                carGeometry: scenario.ConsistDefinition.CarGeometry,
                bogieLayout: scenario.ConsistDefinition.BogieLayout,
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 2,
                    wheelRadius: 0.45,
                    wheelWidth: 0.25,
                    axleSpacing: 1.1));
        }

        private static DebugViewportBoxSource[] BuildTrainBodyBoxes(TrainPoseResult trainPose)
        {
            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = trainPose.CarsReadOnly;
            TrainCarGeometry geometry = trainPose.Definition.CarGeometry;
            var boxes = new DebugViewportBoxSource[cars.Count];

            for (int i = 0; i < cars.Count; i++)
            {
                ArticulatedTrainCarTransform body = cars[i].Body;
                boxes[i] = new DebugViewportBoxSource(
                    role: "train.body",
                    label: "car-" + i,
                    frame: body.ArticulatedFrame,
                    length: geometry.Length,
                    width: geometry.Width,
                    height: geometry.Height);
            }

            return boxes;
        }
    }
}
