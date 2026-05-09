using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Debug
{
    public sealed class SamplingPerfSmokeScenario
    {
        private SamplingPerfSmokeScenario(
            TrackDocument document,
            TrackEvaluator evaluator,
            TrainCarTransformProvider provider,
            double[] distances,
            double leadDistance,
            double carSpacing,
            int carCount,
            TrainConsistDefinition consistDefinition)
        {
            Document = document;
            Evaluator = evaluator;
            Provider = provider;
            Distances = distances;
            LeadDistance = leadDistance;
            CarSpacing = carSpacing;
            CarCount = carCount;
            ConsistDefinition = consistDefinition;
        }

        public TrackDocument Document { get; }

        public TrackEvaluator Evaluator { get; }

        public TrainCarTransformProvider Provider { get; }

        public double[] Distances { get; }

        public double LeadDistance { get; }

        public double CarSpacing { get; }

        public int CarCount { get; }

        public TrainConsistDefinition ConsistDefinition { get; }

        public static SamplingPerfSmokeScenario CreateDeterministic()
        {
            TrackSegment[] segments =
            {
                new StraightSegment(
                    length: 60.0,
                    id: "s0",
                    spline: new LineCurve(
                        new Vector3d(0.0, 0.0, 0.0),
                        new Vector3d(60.0, 0.0, 0.0))),
                new CurvedSegment(
                    length: 100.0,
                    id: "c1",
                    spline: new CubicBezierCurve(
                        new Vector3d(60.0, 0.0, 0.0),
                        new Vector3d(90.0, 6.0, 2.0),
                        new Vector3d(130.0, 14.0, 5.0),
                        new Vector3d(160.0, 20.0, 8.0)),
                    rollRadians: 0.2),
                new StraightSegment(
                    length: 60.0,
                    id: "s2",
                    spline: new LineCurve(
                        new Vector3d(160.0, 20.0, 8.0),
                        new Vector3d(220.0, 20.0, 8.0)))
            };

            var document = new TrackDocument(segments);
            var evaluator = new TrackEvaluator(document);
            var provider = new TrainCarTransformProvider(evaluator);

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
                bogieSpacing: 4.0);

            const double leadDistance = 150.0;

            return new SamplingPerfSmokeScenario(
                document,
                evaluator,
                provider,
                distances,
                leadDistance,
                carSpacing,
                carCount,
                consistDefinition);
        }
    }
}
