using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Debug
{
    public sealed class SamplingPerfSmokeScenario
    {
        private SamplingPerfSmokeScenario(
            TrackDocument document,
            CompiledTrackRuntime runtime,
            TrackEvaluator documentEvaluator,
            TrackEvaluator runtimeEvaluator,
            TrainCarTransformProvider documentProvider,
            TrainCarTransformProvider runtimeProvider,
            double[] distances,
            double leadDistance,
            double carSpacing,
            int carCount,
            TrainConsistDefinition consistDefinition)
        {
            Document = document;
            Runtime = runtime;
            DocumentEvaluator = documentEvaluator;
            RuntimeEvaluator = runtimeEvaluator;
            DocumentProvider = documentProvider;
            RuntimeProvider = runtimeProvider;
            Distances = distances;
            LeadDistance = leadDistance;
            CarSpacing = carSpacing;
            CarCount = carCount;
            ConsistDefinition = consistDefinition;
        }

        public TrackDocument Document { get; }

        public CompiledTrackRuntime Runtime { get; }

        public TrackEvaluator DocumentEvaluator { get; }

        public TrackEvaluator RuntimeEvaluator { get; }

        public TrainCarTransformProvider DocumentProvider { get; }

        public TrainCarTransformProvider RuntimeProvider { get; }

        public TrackEvaluator Evaluator => DocumentEvaluator;

        public TrainCarTransformProvider Provider => DocumentProvider;

        public double[] Distances { get; }

        public double LeadDistance { get; }

        public double CarSpacing { get; }

        public int CarCount { get; }

        public TrainConsistDefinition ConsistDefinition { get; }

        public static SamplingPerfSmokeScenario CreateDeterministic()
        {
            var middleCurve = new CubicBezierCurve(
                new Vector3d(60.0, 0.0, 0.0),
                new Vector3d(90.0, 6.0, 2.0),
                new Vector3d(130.0, 14.0, 5.0),
                new Vector3d(160.0, 20.0, 8.0));
            TrackSegment[] segments =
            {
                new StraightSegment(
                    length: 60.0,
                    id: "s0",
                    spline: new LineCurve(
                        new Vector3d(0.0, 0.0, 0.0),
                        new Vector3d(60.0, 0.0, 0.0))),
                new CurvedSegment(
                    length: new ArcLengthLUT(middleCurve).TotalLength,
                    id: "c1",
                    spline: middleCurve,
                    rollRadians: 0.2),
                new StraightSegment(
                    length: 60.0,
                    id: "s2",
                    spline: new LineCurve(
                        new Vector3d(160.0, 20.0, 8.0),
                        new Vector3d(220.0, 20.0, 8.0)))
            };

            var document = new TrackDocument(segments);
            var runtime = new CompiledTrackRuntime(document);
            var documentEvaluator = new TrackEvaluator(document);
            var runtimeEvaluator = new TrackEvaluator(runtime);
            var documentProvider = new TrainCarTransformProvider(documentEvaluator);
            var runtimeProvider = new TrainCarTransformProvider(runtimeEvaluator);

            const int distanceSampleCount = 512;
            double totalLength = document.TotalLength;
            var distances = new double[distanceSampleCount];

            for (int i = 0; i < distanceSampleCount; i++)
            {
                double fraction = i / (double)(distanceSampleCount - 1);
                distances[i] = totalLength * fraction;
            }

            const int carCount = 8;
            const double carSpacing = 6.0;

            var consistDefinition = new TrainConsistDefinition(
                carCount: carCount,
                carSpacing: carSpacing,
                carLength: 8.0,
                carWidth: 1.8,
                carHeight: 2.2,
                bogieSpacing: 4.0,
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 4,
                    wheelRadius: 0.45,
                    wheelWidth: 0.3,
                    axleSpacing: 1.2));

            const double leadDistance = 150.0;

            return new SamplingPerfSmokeScenario(
                document,
                runtime,
                documentEvaluator,
                runtimeEvaluator,
                documentProvider,
                runtimeProvider,
                distances,
                leadDistance,
                carSpacing,
                carCount,
                consistDefinition);
        }
    }
}
