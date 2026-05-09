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
                    name: "track_scalar",
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.Distances.Length,
                    execute: () => EvaluateTrackScalar(scenario)),
                RunBenchmark(
                    name: "track_batch",
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.Distances.Length,
                    execute: () => EvaluateTrackBatch(scenario)),
                RunBenchmark(
                    name: "body_batch",
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount,
                    execute: () => EvaluateBodyBatch(scenario)),
                RunBenchmark(
                    name: "bogie_batch",
                    warmupIterations,
                    timedIterations,
                    operationsPerIteration: scenario.CarCount * 3,
                    execute: () => EvaluateBogieBatch(scenario))
            };
        }

        private static SamplingPerfBenchmarkResult RunBenchmark(
            string name,
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

        private static double EvaluateBodyBatch(SamplingPerfSmokeScenario scenario)
        {
            double checksum = 0.0;
            IReadOnlyList<TrainCarTransform> cars = scenario.Provider.GetCarTransforms(
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

        private static double EvaluateBogieBatch(SamplingPerfSmokeScenario scenario)
        {
            double checksum = 0.0;
            IReadOnlyList<TrainCarWithBogiesTransform> cars = scenario.Provider.EvaluateTrainWithBogies(
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

        private static void WriteTable(IReadOnlyList<SamplingPerfBenchmarkResult> results)
        {
            string[] headers =
            {
                "benchmark",
                "warmup",
                "iterations",
                "ops/iter",
                "mean_ms",
                "min_ms",
                "max_ms",
                "throughput_ops_s",
                "checksum"
            };

            var rows = new string[results.Count][];
            for (int i = 0; i < results.Count; i++)
            {
                SamplingPerfBenchmarkResult result = results[i];
                rows[i] = new[]
                {
                    result.Name,
                    result.WarmupIterations.ToString(CultureInfo.InvariantCulture),
                    result.TimedIterations.ToString(CultureInfo.InvariantCulture),
                    result.OperationsPerIteration.ToString(CultureInfo.InvariantCulture),
                    result.Timing.MeanMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.Timing.MinMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.Timing.MaxMilliseconds.ToString("0.000", CultureInfo.InvariantCulture),
                    result.ThroughputOperationsPerSecond.ToString("0.0", CultureInfo.InvariantCulture),
                    result.Checksum.ToString("G17", CultureInfo.InvariantCulture)
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
            int warmupIterations,
            int timedIterations,
            int operationsPerIteration,
            SamplingPerfTimingStats timing,
            double throughputOperationsPerSecond,
            double checksum)
        {
            Name = name;
            WarmupIterations = warmupIterations;
            TimedIterations = timedIterations;
            OperationsPerIteration = operationsPerIteration;
            Timing = timing;
            ThroughputOperationsPerSecond = throughputOperationsPerSecond;
            Checksum = checksum;
        }

        public string Name { get; }

        public int WarmupIterations { get; }

        public int TimedIterations { get; }

        public int OperationsPerIteration { get; }

        public SamplingPerfTimingStats Timing { get; }

        public double ThroughputOperationsPerSecond { get; }

        public double Checksum { get; }
    }
}
