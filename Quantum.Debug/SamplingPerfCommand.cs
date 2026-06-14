using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class SamplingPerfCommand
    {
        private const int WarmupIterations = 6;
        private const int TimedIterations = 20;
        private const string SmokeScenarioName = "smoke";
        private const string TrackScalarBenchmarkName = "track_scalar";
        private const string TrackBatchBenchmarkName = "track_batch";
        private const string BodyBatchDocumentBenchmarkName = "body_batch_document";
        private const string BodyBatchRuntimeBenchmarkName = "body_batch_runtime";
        private const string BogieBatchDocumentBenchmarkName = "bogie_batch_document";
        private const string BogieBatchRuntimeBenchmarkName = "bogie_batch_runtime";
        private const string TrainPoseDocumentBenchmarkName = "train_pose_document";
        private const string TrainPoseRuntimeBenchmarkName = "train_pose_runtime";

        public static void Run()
        {
            SamplingPerfSmokeScenario scenario = SamplingPerfSmokeScenario.CreateDeterministic();
            SamplingPerfBenchmarkResult[] results = RunBenchmarks(
                scenario,
                WarmupIterations,
                TimedIterations);
            WriteTable(results);
        }

        internal static SamplingPerfBenchmarkResult[] RunBenchmarks(
            SamplingPerfSmokeScenario scenario,
            int warmupIterations,
            int timedIterations)
        {
            if (scenario is null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (warmupIterations < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupIterations), "Warmup iterations must be non-negative.");
            }

            if (timedIterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timedIterations), "Timed iterations must be greater than zero.");
            }

            return new[]
            {
                RunBenchmark(
                    name: TrackScalarBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.Distances.Length,
                    execute: () => EvaluateTrackScalar(scenario)),
                RunBenchmark(
                    name: TrackBatchBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.Distances.Length,
                    execute: () => EvaluateTrackBatch(scenario)),
                RunBenchmark(
                    name: BodyBatchDocumentBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount,
                    execute: () => EvaluateBodyBatch(scenario, scenario.DocumentProvider)),
                RunBenchmark(
                    name: BodyBatchRuntimeBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount,
                    execute: () => EvaluateBodyBatch(scenario, scenario.RuntimeProvider)),
                RunBenchmark(
                    name: BogieBatchDocumentBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount * 3,
                    execute: () => EvaluateBogieBatch(scenario, scenario.DocumentProvider)),
                RunBenchmark(
                    name: BogieBatchRuntimeBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount * 3,
                    execute: () => EvaluateBogieBatch(scenario, scenario.RuntimeProvider)),
                RunBenchmark(
                    name: TrainPoseDocumentBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount,
                    execute: () => EvaluateTrainPose(scenario, scenario.DocumentProvider)),
                RunBenchmark(
                    name: TrainPoseRuntimeBenchmarkName,
                    scenario: SmokeScenarioName,
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount,
                    execute: () => EvaluateTrainPose(scenario, scenario.RuntimeProvider))
            };
        }

        private static SamplingPerfBenchmarkResult RunBenchmark(
            string name,
            string scenario,
            int warmupIterations,
            int timedIterations,
            int operationsPerIteration,
            Func<double> execute)
        {
            var stopwatch = new Stopwatch();
            double checksum = 0.0;
            var samples = new double[timedIterations];

            for (int i = 0; i < warmupIterations; i++)
            {
                checksum += execute();
            }

            for (int i = 0; i < timedIterations; i++)
            {
                stopwatch.Restart();
                checksum += execute();
                stopwatch.Stop();

                samples[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            SamplingPerfTimingStats stats = SamplingPerfTimingStats.Compute(samples);
            double throughput = stats.ComputeThroughputOperationsPerSecond(operationsPerIteration);

            return new SamplingPerfBenchmarkResult(
                name,
                scenario,
                warmupIterations,
                timedIterations,
                operationsPerIteration,
                stats,
                throughput,
                checksum);
        }

        private static double EvaluateTrackScalar(SamplingPerfSmokeScenario scenario)
        {
            double checksum = 0.0;
            TrackEvaluator evaluator = scenario.Evaluator;
            double[] distances = scenario.Distances;

            for (int i = 0; i < distances.Length; i++)
            {
                TrackFrame frame = evaluator.EvaluateFrameAtDistance(distances[i]);
                checksum += frame.Position.X + frame.Position.Y + frame.Position.Z;
                checksum += frame.Tangent.X + frame.Tangent.Y + frame.Tangent.Z;
            }

            return checksum;
        }

        private static double EvaluateTrackBatch(SamplingPerfSmokeScenario scenario)
        {
            double checksum = 0.0;
            TrackFrame[] frames = scenario.Evaluator.EvaluateFramesAtDistances(scenario.Distances);

            for (int i = 0; i < frames.Length; i++)
            {
                TrackFrame frame = frames[i];
                checksum += frame.Position.X + frame.Position.Y + frame.Position.Z;
                checksum += frame.Tangent.X + frame.Tangent.Y + frame.Tangent.Z;
            }

            return checksum;
        }

        private static double EvaluateBodyBatch(
            SamplingPerfSmokeScenario scenario,
            TrainCarTransformProvider provider)
        {
            double checksum = 0.0;
            IReadOnlyList<TrainCarTransform> cars = provider.EvaluateCarTransforms(
                scenario.LeadDistance,
                scenario.CarSpacing,
                scenario.CarCount);

            for (int i = 0; i < cars.Count; i++)
            {
                TrainCarTransform car = cars[i];
                checksum += car.Distance + car.Frame.Position.X + car.Frame.Position.Y + car.Frame.Position.Z;
                checksum += car.Frame.Tangent.X + car.Frame.Tangent.Y + car.Frame.Tangent.Z;
            }

            return checksum;
        }

        private static double EvaluateBogieBatch(
            SamplingPerfSmokeScenario scenario,
            TrainCarTransformProvider provider)
        {
            double checksum = 0.0;
            IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
                scenario.LeadDistance,
                scenario.ConsistDefinition);

            for (int i = 0; i < cars.Count; i++)
            {
                TrainCarWithBogiesTransform car = cars[i];
                checksum += car.Body.Distance + car.Body.Frame.Position.X + car.Body.Frame.Position.Y + car.Body.Frame.Position.Z;
                checksum += car.FrontBogie.Distance + car.FrontBogie.Frame.Position.X + car.FrontBogie.Frame.Position.Y + car.FrontBogie.Frame.Position.Z;
                checksum += car.RearBogie.Distance + car.RearBogie.Frame.Position.X + car.RearBogie.Frame.Position.Y + car.RearBogie.Frame.Position.Z;
            }

            return checksum;
        }

        private static double EvaluateTrainPose(
            SamplingPerfSmokeScenario scenario,
            TrainCarTransformProvider provider)
        {
            TrainPoseResult pose = provider.EvaluateTrainPose(
                scenario.LeadDistance,
                scenario.ConsistDefinition);
            double checksum = pose.LeadDistance;

            for (int i = 0; i < pose.CarsReadOnly.Count; i++)
            {
                ArticulatedTrainCarWithWheelsTransform car = pose.CarsReadOnly[i];
                checksum += SumFrame(car.Body.ArticulatedFrame);
                checksum += SumFrame(car.FrontBogie.Bogie.Frame);
                checksum += SumFrame(car.RearBogie.Bogie.Frame);

                for (int wheelIndex = 0; wheelIndex < car.FrontBogie.WheelsReadOnly.Count; wheelIndex++)
                {
                    checksum += SumFrame(car.FrontBogie.WheelsReadOnly[wheelIndex].Frame);
                    checksum += SumFrame(car.RearBogie.WheelsReadOnly[wheelIndex].Frame);
                }
            }

            return checksum;
        }

        private static double SumFrame(TrackFrame frame)
        {
            return frame.Distance +
                   frame.Position.X + frame.Position.Y + frame.Position.Z +
                   frame.Tangent.X + frame.Tangent.Y + frame.Tangent.Z;
        }

        private static void WriteTable(IReadOnlyList<SamplingPerfBenchmarkResult> results)
        {
            string[] headers =
            {
                "benchmark",
                "scenario",
                "mean_ms",
                "min_ms",
                "max_ms",
                "throughput",
                "checksum",
                "relative_speedup"
            };

            var resultsByBenchmark = new Dictionary<string, SamplingPerfBenchmarkResult>(StringComparer.Ordinal);
            for (int i = 0; i < results.Count; i++)
            {
                SamplingPerfBenchmarkResult result = results[i];
                resultsByBenchmark[result.Name] = result;
            }

            var rows = new string[results.Count][];
            for (int i = 0; i < results.Count; i++)
            {
                SamplingPerfBenchmarkResult result = results[i];
                rows[i] = new[]
                {
                    result.Name,
                    result.Scenario,
                    result.Timing.MeanMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.Timing.MinMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.Timing.MaxMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.ThroughputOperationsPerSecond.ToString("0.0", CultureInfo.InvariantCulture),
                    result.Checksum.ToString("G17", CultureInfo.InvariantCulture),
                    FormatRelativeSpeedup(result, resultsByBenchmark)
                };
            }

            int columnCount = headers.Length;
            var widths = new int[columnCount];

            for (int i = 0; i < columnCount; i++)
            {
                widths[i] = headers[i].Length;
            }

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    widths[columnIndex] = System.Math.Max(widths[columnIndex], rows[rowIndex][columnIndex].Length);
                }
            }

            string border = BuildBorder(widths);
            Console.WriteLine(border);
            Console.WriteLine(BuildRow(headers, widths));
            Console.WriteLine(border);

            for (int i = 0; i < rows.Length; i++)
            {
                Console.WriteLine(BuildRow(rows[i], widths));
            }

            Console.WriteLine(border);
        }

        private static string FormatRelativeSpeedup(
            SamplingPerfBenchmarkResult result,
            IReadOnlyDictionary<string, SamplingPerfBenchmarkResult> resultsByBenchmark)
        {
            if (string.Equals(result.Name, TrackScalarBenchmarkName, StringComparison.Ordinal))
            {
                return "baseline";
            }

            if (string.Equals(result.Name, TrackBatchBenchmarkName, StringComparison.Ordinal) &&
                resultsByBenchmark.TryGetValue(TrackScalarBenchmarkName, out SamplingPerfBenchmarkResult baseline))
            {
                double factor = result.Timing.ComputeRelativeSpeedupAgainst(baseline.Timing);
                return FormatRelativeSpeedupFactor(factor);
            }

            string? documentBaselineName = result.Name switch
            {
                BodyBatchRuntimeBenchmarkName => BodyBatchDocumentBenchmarkName,
                BogieBatchRuntimeBenchmarkName => BogieBatchDocumentBenchmarkName,
                TrainPoseRuntimeBenchmarkName => TrainPoseDocumentBenchmarkName,
                _ => null
            };

            if (documentBaselineName != null &&
                resultsByBenchmark.TryGetValue(documentBaselineName, out SamplingPerfBenchmarkResult documentBaseline))
            {
                double factor = result.Timing.ComputeRelativeSpeedupAgainst(documentBaseline.Timing);
                return FormatRelativeSpeedupFactor(factor);
            }

            return "-";
        }

        private static string FormatRelativeSpeedupFactor(double factor)
        {
            if (double.IsNaN(factor) || factor < 0.0)
            {
                return "n/a";
            }

            if (double.IsPositiveInfinity(factor))
            {
                return "infx faster";
            }

            if (factor == 0.0)
            {
                return "infx slower";
            }

            if (factor >= 1.0)
            {
                return factor.ToString("0.###", CultureInfo.InvariantCulture) + "x faster";
            }

            double slowdownFactor = 1.0 / factor;
            return slowdownFactor.ToString("0.###", CultureInfo.InvariantCulture) + "x slower";
        }

        private static string BuildBorder(IReadOnlyList<int> widths)
        {
            var parts = new string[widths.Count];
            for (int i = 0; i < widths.Count; i++)
            {
                parts[i] = new string('-', widths[i] + 2);
            }

            return "+" + string.Join("+", parts) + "+";
        }

        private static string BuildRow(IReadOnlyList<string> values, IReadOnlyList<int> widths)
        {
            var parts = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                parts[i] = " " + values[i].PadRight(widths[i]) + " ";
            }

            return "|" + string.Join("|", parts) + "|";
        }
    }

    internal readonly struct SamplingPerfBenchmarkResult
    {
        public SamplingPerfBenchmarkResult(
            string name,
            string scenario,
            int warmupIterations,
            int timedIterations,
            int operationsPerIteration,
            SamplingPerfTimingStats timing,
            double throughputOperationsPerSecond,
            double checksum)
        {
            Name = name;
            Scenario = scenario;
            WarmupIterations = warmupIterations;
            TimedIterations = timedIterations;
            OperationsPerIteration = operationsPerIteration;
            Timing = timing;
            ThroughputOperationsPerSecond = throughputOperationsPerSecond;
            Checksum = checksum;
        }

        public string Name { get; }

        public string Scenario { get; }

        public int WarmupIterations { get; }

        public int TimedIterations { get; }

        public int OperationsPerIteration { get; }

        public SamplingPerfTimingStats Timing { get; }

        public double ThroughputOperationsPerSecond { get; }

        public double Checksum { get; }
    }
}
