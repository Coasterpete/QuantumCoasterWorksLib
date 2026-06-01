using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Debug
{
    public sealed class BankingProfileTrainPoseFixture
    {
        public BankingProfileTrainPoseFixture(
            string name,
            TrackDocument document,
            BankingProfile bankingProfile,
            TrainConsistDefinition definition,
            double leadDistance,
            IReadOnlyList<double> centerlineSampleDistances)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Fixture name cannot be empty.", nameof(name));
            }

            if (centerlineSampleDistances is null)
            {
                throw new ArgumentNullException(nameof(centerlineSampleDistances));
            }

            double[] sampleDistances = centerlineSampleDistances.ToArray();
            if (sampleDistances.Length == 0)
            {
                throw new ArgumentException("Fixture sample distances cannot be empty.", nameof(centerlineSampleDistances));
            }

            Name = name;
            Document = document ?? throw new ArgumentNullException(nameof(document));
            BankingProfile = bankingProfile ?? throw new ArgumentNullException(nameof(bankingProfile));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            LeadDistance = leadDistance;
            CenterlineSampleDistances = Array.AsReadOnly(sampleDistances);
        }

        public string Name { get; }

        public TrackDocument Document { get; }

        public BankingProfile BankingProfile { get; }

        public TrainConsistDefinition Definition { get; }

        public double LeadDistance { get; }

        public IReadOnlyList<double> CenterlineSampleDistances { get; }

        public TrainPoseResult EvaluateTrainPose()
        {
            var evaluator = new TrackEvaluator(Document);
            var provider = new TrainCarTransformProvider(evaluator);
            return provider.EvaluateTrainPose(
                LeadDistance,
                Definition,
                BankingProfile);
        }

        public TrainPoseExportV1Dto ExportTrainPose()
        {
            return TrainPoseExportV1Mapper.Export(EvaluateTrainPose());
        }

        public DebugViewportSnapshotV1Dto BuildDebugViewportSnapshot()
        {
            ExportTrackFrame[] sampledFrames = BankingProfileSampler.SampleFramesAtDistances(
                Document,
                BankingProfile,
                CenterlineSampleDistances);
            TrainPoseResult trainPose = EvaluateTrainPose();
            DebugLineSegment[] lines = TrackFrameDebugGizmoBuilder.BuildAxes(
                sampledFrames[sampledFrames.Length / 2],
                axisLength: 4.0);

            var source = new DebugViewportSnapshotV1Source
            {
                Units = "meters",
                SourceFixtureName = Name,
                SampledFrames = sampledFrames,
                Lines = lines,
                Boxes = BuildTrainBodyBoxes(trainPose),
                TrainPose = trainPose
            };

            return DebugViewportSnapshotV1Mapper.Export(source);
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
                    role: DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole,
                    label: "bp-car-" + i,
                    frame: body.ArticulatedFrame,
                    length: geometry.Length,
                    width: geometry.Width,
                    height: geometry.Height);
            }

            return boxes;
        }
    }

    public static class BankingProfileTrainPoseFixtures
    {
        public const string ProfileBackedTrainPoseName = "banking-profile-train-pose";

        public static BankingProfileTrainPoseFixture ProfileBackedTrainPose()
        {
            TrackDocument document = BuildDocument();

            return new BankingProfileTrainPoseFixture(
                ProfileBackedTrainPoseName,
                document,
                BuildProfile(document.TotalLength),
                BuildDefinition(),
                leadDistance: 49.5,
                centerlineSampleDistances: BuildUniformDistances(document.TotalLength, sampleCount: 10));
        }

        private static TrackDocument BuildDocument()
        {
            TrackSegment[] segments =
            {
                new StraightSegment(
                    length: 18.0,
                    id: "bp-train-s0",
                    spline: new LineCurve(
                        new Vector3d(0.0, 0.0, 0.0),
                        new Vector3d(18.0, 0.0, 0.0)),
                    rollRadians: -0.35),
                new CurvedSegment(
                    length: 30.0,
                    id: "bp-train-c1",
                    spline: new CubicBezierCurve(
                        new Vector3d(18.0, 0.0, 0.0),
                        new Vector3d(28.0, 3.0, 5.0),
                        new Vector3d(36.0, 5.0, 18.0),
                        new Vector3d(45.0, 2.0, 26.0)),
                    rollRadians: -0.35),
                new StraightSegment(
                    length: 24.0,
                    id: "bp-train-s2",
                    spline: new LineCurve(
                        new Vector3d(45.0, 2.0, 26.0),
                        new Vector3d(67.0, 4.0, 34.0)),
                    rollRadians: -0.35)
            };

            return new TrackDocument(segments);
        }

        private static BankingProfile BuildProfile(double totalLength)
        {
            return new BankingProfile(new[]
            {
                new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(18.0, ToRadians(18.0), BankingProfileInterpolationMode.SmoothStep),
                new BankingProfileKey(42.0, ToRadians(-22.0), BankingProfileInterpolationMode.Linear),
                new BankingProfileKey(totalLength, ToRadians(34.0), BankingProfileInterpolationMode.Constant)
            });
        }

        private static TrainConsistDefinition BuildDefinition()
        {
            return new TrainConsistDefinition(
                carCount: 3,
                carSpacing: 7.75,
                carLength: 5.4,
                carWidth: 1.65,
                carHeight: 2.15,
                bogieSpacing: 3.2,
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 4,
                    wheelRadius: 0.46,
                    wheelWidth: 0.38,
                    axleSpacing: 1.35));
        }

        private static double[] BuildUniformDistances(double totalLength, int sampleCount)
        {
            if (sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount),
                    sampleCount,
                    "Sample count must be at least two.");
            }

            var distances = new double[sampleCount];
            double interval = totalLength / (sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                distances[i] = i * interval;
            }

            distances[sampleCount - 1] = totalLength;
            return distances;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * System.Math.PI / 180.0;
        }
    }
}
