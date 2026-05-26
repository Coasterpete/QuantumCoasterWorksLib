using System;
using System.Collections.Generic;
using System.Globalization;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;

namespace Quantum.Debug
{
    class Program
    {
        private static readonly double[] SampleTValues = { 0.0, 0.25, 0.5, 0.75, 1.0 };
        private const double NormalizedTolerance = 1e-3;

        static int Main(string[] args)
        {
            if (!DebugCommandParser.TryParse(args, out DebugCommandKind command))
            {
                Console.WriteLine("Unknown command.");
                Console.WriteLine("Supported commands:");
                Console.WriteLine("  sampling-perf");
                Console.WriteLine("  train-pose-export-v1 [outputPath]");
                Console.WriteLine("  debug-viewport-snapshot-v1 [outputPath]");
                Console.WriteLine("  debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]");
                Console.WriteLine("  debug-viewport-snapshot-v1-validate <snapshotJsonPath>");
                Console.WriteLine("  longitudinal-force-preview [preset] [outputPath]");
                Console.WriteLine("    presets: soft | balanced | punchy");
                Console.WriteLine("  longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]");
                Console.WriteLine("    presets: soft | balanced | punchy");
                return 1;
            }

            if (command == DebugCommandKind.SamplingPerf)
            {
                SamplingPerfCommand.Run();
                return 0;
            }

            if (command == DebugCommandKind.TrainPoseExportV1)
            {
                if (args.Length > 2)
                {
                    Console.WriteLine("Usage: train-pose-export-v1 [outputPath]");
                    return 1;
                }

                string? outputPath = args.Length == 2 ? args[1] : null;
                return TrainPoseExportV1Command.Run(outputPath);
            }

            if (command == DebugCommandKind.DebugViewportSnapshotV1)
            {
                if (args.Length > 2)
                {
                    Console.WriteLine("Usage: debug-viewport-snapshot-v1 [outputPath]");
                    return 1;
                }

                string? outputPath = args.Length == 2 ? args[1] : null;
                return DebugViewportSnapshotV1SampleCommand.Run(outputPath);
            }

            if (command == DebugCommandKind.DebugViewportSnapshotV1FromCsv)
            {
                if (args.Length < 2 || args.Length > 3)
                {
                    Console.WriteLine("Usage: debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]");
                    return 1;
                }

                string inputCsvPath = args[1];
                string? outputJsonPath = args.Length == 3 ? args[2] : null;
                return DebugViewportSnapshotV1FromCsvCommand.Run(inputCsvPath, outputJsonPath);
            }

            if (command == DebugCommandKind.DebugViewportSnapshotV1Validate)
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Usage: debug-viewport-snapshot-v1-validate <snapshotJsonPath>");
                    return 1;
                }

                return DebugViewportSnapshotV1ValidateCommand.Run(args[1]);
            }

            if (command == DebugCommandKind.LongitudinalForcePreview)
            {
                if (!TryParseLongitudinalForcePreviewArgs(args, out LongitudinalForcePreviewPreset preset, out string? outputPath))
                {
                    Console.WriteLine("Usage: longitudinal-force-preview [preset] [outputPath]");
                    Console.WriteLine("  - no args after command: uses balanced preset and default output path");
                    Console.WriteLine("  - one arg: preset name OR output path");
                    Console.WriteLine("  - two args: preset name then output path");
                    Console.WriteLine("  - presets: soft | balanced | punchy");
                    return 1;
                }

                return LongitudinalForcePreviewCommand.Run(outputPath, preset);
            }

            if (command == DebugCommandKind.LongitudinalSpeedPreview)
            {
                if (!TryParseLongitudinalSpeedPreviewArgs(
                        args,
                        out LongitudinalForcePreviewPreset preset,
                        out string? outputPath,
                        out double initialSpeedMps))
                {
                    Console.WriteLine("Usage: longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]");
                    Console.WriteLine("  - no args after command: uses balanced preset, default output path, and initialSpeedMps=0");
                    Console.WriteLine("  - one arg: preset name OR output path");
                    Console.WriteLine("  - two args: [preset outputPath] OR [preset initialSpeedMps] OR [outputPath initialSpeedMps]");
                    Console.WriteLine("  - three args: preset name, output path, then initialSpeedMps");
                    Console.WriteLine("  - presets: soft | balanced | punchy");
                    Console.WriteLine("  - initialSpeedMps must be finite and >= 0");
                    return 1;
                }

                return LongitudinalSpeedPreviewCommand.Run(outputPath, preset, initialSpeedMps);
            }

            RunValidation();
            return 0;
        }

        private static void RunValidation()
        {
            Console.WriteLine("=== Quantum Debug ===");

            int paramErrorCount = 0;
            var curveTests = BuildCurveTests();

            foreach (var (name, curve) in curveTests)
            {
                Console.WriteLine($"\n=== {name} ===");

                foreach (double t in SampleTValues)
                {
                    paramErrorCount += ValidateParamSample(name, curve, t);
                }
            }

            int distanceErrorCount = ValidateDistanceCurveTests();
            int followerErrorCount = ValidateTrainFollowerTests();
            int totalErrors = paramErrorCount + distanceErrorCount + followerErrorCount;

            Console.WriteLine("\n=== Validation Summary ===");
            if (totalErrors == 0)
            {
                Console.WriteLine("All spline and follower validation checks passed.");
            }
            else
            {
                Console.WriteLine($"Validation completed with {totalErrors} error(s).");
                Console.WriteLine("See [ERROR] lines above for curve and sample details.");
            }
        }

        private static List<(string Name, IParamCurve Curve)> BuildCurveTests()
        {
            var start = new Vector3d(0, 0, 0);
            var end = new Vector3d(10, 0, 0);
            IParamCurve line = new LineCurve(start, end);

            var q0 = new Vector3d(0, 0, 0);
            var q1 = new Vector3d(5, 5, 0);
            var q2 = new Vector3d(10, 0, 0);
            IParamCurve quadratic = new QuadraticBezierCurve(q0, q1, q2);

            var c0 = new Vector3d(0, 0, 0);
            var c1 = new Vector3d(3, 6, 0);
            var c2 = new Vector3d(7, -6, 0);
            var c3 = new Vector3d(10, 0, 0);
            IParamCurve cubic = new CubicBezierCurve(c0, c1, c2, c3);

            var points = new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            };
            IParamCurve bspline = new BSplineCurve(points, 3);

            return new List<(string Name, IParamCurve Curve)>
            {
                ("Line Curve", line),
                ("Quadratic Bezier", quadratic),
                ("Cubic Bezier", cubic),
                ("B-Spline (Degree 3)", bspline)
            };
        }

        private static int ValidateDistanceCurveTests()
        {
            Console.WriteLine("\n=== Arc-Length Adapter Validation ===");

            int errors = 0;
            var distanceTests = BuildDistanceCurveTests();

            foreach (var (name, curve) in distanceTests)
            {
                Console.WriteLine($"\n--- {name} ---");
                Console.WriteLine($"Length={curve.Length:0.000000}");

                foreach (double fraction in SampleTValues)
                {
                    double s = curve.Length * fraction;
                    errors += ValidateDistanceSample(name, curve, s);
                }
            }

            return errors;
        }

        private static List<(string Name, IArcLengthCurve Curve)> BuildDistanceCurveTests()
        {
            var c0 = new Vector3d(0, 0, 0);
            var c1 = new Vector3d(3, 6, 0);
            var c2 = new Vector3d(7, -6, 0);
            var c3 = new Vector3d(10, 0, 0);
            IParamCurve cubic = new CubicBezierCurve(c0, c1, c2, c3);

            var points = new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            };
            IParamCurve bspline = new BSplineCurve(points, 3);

            return new List<(string Name, IArcLengthCurve Curve)>
            {
                ("Cubic Bezier (ArcLength Adapter)", new ArcLengthCurveAdapter(cubic, samples: 200)),
                ("B-Spline (ArcLength Adapter)", new ArcLengthCurveAdapter(bspline, samples: 200))
            };
        }

        private static int ValidateTrainFollowerTests()
        {
            Console.WriteLine("\n=== Train Follower Validation ===");

            int errors = 0;
            var followerTests = BuildFollowerTests();

            foreach (var (name, follower, deltaTime, steps) in followerTests)
            {
                Console.WriteLine($"\n--- {name} ---");
                Console.WriteLine(
                    $"TrackLength={follower.Track.Length:0.000000}, Speed={follower.Speed:0.000}, Loop={follower.LoopEnabled}");

                errors += PrintFollowerSample(name, "step=0", follower);

                for (int i = 1; i <= steps; i++)
                {
                    follower.Update(deltaTime);
                    errors += PrintFollowerSample(name, $"step={i}", follower);
                }
            }

            return errors;
        }

        private static List<(string Name, TrainFollowerState Follower, double DeltaTime, int Steps)> BuildFollowerTests()
        {
            var c0 = new Vector3d(0, 0, 0);
            var c1 = new Vector3d(3, 6, 0);
            var c2 = new Vector3d(7, -6, 0);
            var c3 = new Vector3d(10, 0, 0);
            IParamCurve cubic = new CubicBezierCurve(c0, c1, c2, c3);
            IArcLengthCurve cubicTrack = new ArcLengthCurveAdapter(cubic, samples: 200);

            var points = new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            };
            IParamCurve bspline = new BSplineCurve(points, 3);
            IArcLengthCurve bsplineTrack = new ArcLengthCurveAdapter(bspline, samples: 200);

            return new List<(string Name, TrainFollowerState Follower, double DeltaTime, int Steps)>
            {
                (
                    "Follower On Cubic Bezier (No Loop)",
                    new TrainFollowerState(cubicTrack, initialDistance: 0.0, speed: 2.0, loopEnabled: false),
                    0.5,
                    8),
                (
                    "Follower On B-Spline (Loop)",
                    new TrainFollowerState(bsplineTrack, initialDistance: bsplineTrack.Length - 1.0, speed: 2.5, loopEnabled: true),
                    0.5,
                    8)
            };
        }

        private static bool TryParseLongitudinalForcePreviewArgs(
            IReadOnlyList<string> args,
            out LongitudinalForcePreviewPreset preset,
            out string? outputPath)
        {
            preset = LongitudinalForcePreviewPreset.Balanced;
            outputPath = null;

            if (args.Count <= 1)
            {
                return true;
            }

            if (args.Count == 2)
            {
                if (LongitudinalForcePreviewCommand.TryParsePreset(args[1], out LongitudinalForcePreviewPreset parsedPreset))
                {
                    preset = parsedPreset;
                    return true;
                }

                outputPath = args[1];
                return true;
            }

            if (args.Count == 3)
            {
                if (!LongitudinalForcePreviewCommand.TryParsePreset(args[1], out LongitudinalForcePreviewPreset parsedPreset))
                {
                    return false;
                }

                preset = parsedPreset;
                outputPath = args[2];
                return true;
            }

            return false;
        }

        private static bool TryParseLongitudinalSpeedPreviewArgs(
            IReadOnlyList<string> args,
            out LongitudinalForcePreviewPreset preset,
            out string? outputPath,
            out double initialSpeedMps)
        {
            preset = LongitudinalForcePreviewPreset.Balanced;
            outputPath = null;
            initialSpeedMps = 0.0;

            if (args.Count <= 1)
            {
                return true;
            }

            if (args.Count == 2)
            {
                if (LongitudinalForcePreviewCommand.TryParsePreset(args[1], out LongitudinalForcePreviewPreset parsedPreset))
                {
                    preset = parsedPreset;
                    return true;
                }

                outputPath = args[1];
                return true;
            }

            if (args.Count == 3)
            {
                if (LongitudinalForcePreviewCommand.TryParsePreset(args[1], out LongitudinalForcePreviewPreset parsedPreset))
                {
                    preset = parsedPreset;

                    if (TryParseNonNegativeFiniteDouble(args[2], out double parsedInitialSpeed))
                    {
                        initialSpeedMps = parsedInitialSpeed;
                        return true;
                    }

                    outputPath = args[2];
                    return true;
                }

                outputPath = args[1];
                return TryParseNonNegativeFiniteDouble(args[2], out initialSpeedMps);
            }

            if (args.Count == 4)
            {
                if (!LongitudinalForcePreviewCommand.TryParsePreset(args[1], out LongitudinalForcePreviewPreset parsedPreset))
                {
                    return false;
                }

                preset = parsedPreset;
                outputPath = args[2];
                return TryParseNonNegativeFiniteDouble(args[3], out initialSpeedMps);
            }

            return false;
        }

        private static bool TryParseNonNegativeFiniteDouble(string value, out double parsed)
        {
            parsed = 0.0;

            if (!TryParseDouble(value, CultureInfo.InvariantCulture, out parsed) &&
                !TryParseDouble(value, CultureInfo.CurrentCulture, out parsed))
            {
                return false;
            }

            return !double.IsNaN(parsed) && !double.IsInfinity(parsed) && parsed >= 0.0;
        }

        private static bool TryParseDouble(string value, CultureInfo culture, out double parsed)
        {
            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                culture,
                out parsed);
        }

        private static int PrintFollowerSample(string followerName, string sampleLabel, TrainFollowerState follower)
        {
            Vector3d pos = follower.Position;
            Vector3d tan = follower.Tangent;
            double tanLength = tan.Length;

            Console.WriteLine(
                $"{sampleLabel} | s={follower.Distance:0.######} | Pos=({pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}) " +
                $"Tan=({tan.X:0.00}, {tan.Y:0.00}, {tan.Z:0.00}) | |Tan|={tanLength:0.000000}");

            return ValidateTangent(followerName, sampleLabel, tan, tanLength);
        }

        private static int ValidateParamSample(string curveName, IParamCurve curve, double t)
        {
            string sampleLabel = $"t={t:0.00}";

            try
            {
                Vector3d pos = curve.Evaluate(t);
                Vector3d tan = curve.Tangent(t);
                double tanLength = tan.Length;

                Console.WriteLine(
                    $"{sampleLabel} | Pos=({pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}) " +
                    $"Tan=({tan.X:0.00}, {tan.Y:0.00}, {tan.Z:0.00}) | |Tan|={tanLength:0.000000}"
                );

                return ValidateTangent(curveName, sampleLabel, tan, tanLength);
            }
            catch (Exception ex)
            {
                PrintError(curveName, sampleLabel, $"Exception during sample evaluation: {ex.GetType().Name} - {ex.Message}");
                return 1;
            }
        }

        private static int ValidateDistanceSample(string curveName, IArcLengthCurve curve, double s)
        {
            string sampleLabel = $"s={s:0.######}";

            try
            {
                Vector3d pos = curve.EvaluateByLength(s);
                Vector3d tan = curve.TangentByLength(s);
                double tanLength = tan.Length;

                Console.WriteLine(
                    $"{sampleLabel} | Pos=({pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}) " +
                    $"Tan=({tan.X:0.00}, {tan.Y:0.00}, {tan.Z:0.00}) | |Tan|={tanLength:0.000000}"
                );

                return ValidateTangent(curveName, sampleLabel, tan, tanLength);
            }
            catch (Exception ex)
            {
                PrintError(curveName, sampleLabel, $"Exception during distance sample evaluation: {ex.GetType().Name} - {ex.Message}");
                return 1;
            }
        }

        private static int ValidateTangent(string curveName, string sampleLabel, Vector3d tan, double tanLength)
        {
            int errors = 0;

            if (ContainsNaN(tan))
            {
                PrintError(curveName, sampleLabel, "Tangent contains NaN components.");
                errors++;
            }

            if (ContainsInfinity(tan))
            {
                PrintError(curveName, sampleLabel, "Tangent contains Infinity components.");
                errors++;
            }

            if (double.IsNaN(tanLength) || double.IsInfinity(tanLength))
            {
                PrintError(curveName, sampleLabel, $"Tangent length is invalid: {tanLength}.");
                errors++;
            }
            else
            {
                if (tanLength <= MathUtil.Epsilon)
                {
                    PrintError(curveName, sampleLabel, $"Tangent is zero-length or near zero (|Tan|={tanLength:0.######}).");
                    errors++;
                }

                if (System.Math.Abs(tanLength - 1.0) > NormalizedTolerance)
                {
                    PrintError(
                        curveName,
                        sampleLabel,
                        $"Tangent is not normalized (|Tan|={tanLength:0.######}, expected ~1.0 +/- {NormalizedTolerance:0.###}).");
                    errors++;
                }
            }

            return errors;
        }

        private static bool ContainsNaN(Vector3d value)
        {
            return double.IsNaN(value.X) || double.IsNaN(value.Y) || double.IsNaN(value.Z);
        }

        private static bool ContainsInfinity(Vector3d value)
        {
            return double.IsInfinity(value.X) || double.IsInfinity(value.Y) || double.IsInfinity(value.Z);
        }

        private static void PrintError(string curveName, string sampleLabel, string message)
        {
            Console.WriteLine($"[ERROR] {curveName} @ {sampleLabel}: {message}");
        }
    }
}
