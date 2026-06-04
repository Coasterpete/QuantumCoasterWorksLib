using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SystemMath = System.Math;

namespace Quantum.Track
{
    public enum ContinuousRollWrapMode
    {
        None = 0,
        FullTurn = 1
    }

    public enum ContinuousRollWarningKind
    {
        RollDelta = 0,
        RollRate = 1
    }

    public readonly struct ContinuousRollDiagnosticsOptions
    {
        private const double DegreesToRadians = SystemMath.PI / 180.0;

        public ContinuousRollDiagnosticsOptions(
            ContinuousRollWrapMode wrapMode,
            double rollDeltaWarningThresholdRadians,
            double rollRateWarningThresholdRadPerMeter)
        {
            if (!IsSupportedWrapMode(wrapMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wrapMode),
                    wrapMode,
                    "Unsupported continuous roll wrap mode.");
            }

            ValidateFiniteNonNegative(
                rollDeltaWarningThresholdRadians,
                nameof(rollDeltaWarningThresholdRadians));
            ValidateFiniteNonNegative(
                rollRateWarningThresholdRadPerMeter,
                nameof(rollRateWarningThresholdRadPerMeter));

            WrapMode = wrapMode;
            RollDeltaWarningThresholdRadians = rollDeltaWarningThresholdRadians;
            RollRateWarningThresholdRadPerMeter = rollRateWarningThresholdRadPerMeter;
        }

        public ContinuousRollWrapMode WrapMode { get; }

        public double RollDeltaWarningThresholdRadians { get; }

        public double RollRateWarningThresholdRadPerMeter { get; }

        public static ContinuousRollDiagnosticsOptions Default => FromDegrees(
            ContinuousRollWrapMode.FullTurn,
            rollDeltaWarningThresholdDegrees: 45.0,
            rollRateWarningThresholdDegreesPerMeter: 45.0);

        public static ContinuousRollDiagnosticsOptions NoWrap => FromDegrees(
            ContinuousRollWrapMode.None,
            rollDeltaWarningThresholdDegrees: 45.0,
            rollRateWarningThresholdDegreesPerMeter: 45.0);

        public static ContinuousRollDiagnosticsOptions FromDegrees(
            ContinuousRollWrapMode wrapMode,
            double rollDeltaWarningThresholdDegrees,
            double rollRateWarningThresholdDegreesPerMeter)
        {
            ValidateFiniteNonNegative(
                rollDeltaWarningThresholdDegrees,
                nameof(rollDeltaWarningThresholdDegrees));
            ValidateFiniteNonNegative(
                rollRateWarningThresholdDegreesPerMeter,
                nameof(rollRateWarningThresholdDegreesPerMeter));

            return new ContinuousRollDiagnosticsOptions(
                wrapMode,
                rollDeltaWarningThresholdDegrees * DegreesToRadians,
                rollRateWarningThresholdDegreesPerMeter * DegreesToRadians);
        }

        private static bool IsSupportedWrapMode(ContinuousRollWrapMode mode)
        {
            switch (mode)
            {
                case ContinuousRollWrapMode.None:
                case ContinuousRollWrapMode.FullTurn:
                    return true;

                default:
                    return false;
            }
        }

        private static void ValidateFiniteNonNegative(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Threshold must be finite and non-negative.");
            }
        }
    }

    public readonly struct ContinuousRollDiagnosticsSample
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public ContinuousRollDiagnosticsSample(
            int sampleIndex,
            double distance,
            double rollRadians,
            double continuousRollRadians)
        {
            if (sampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleIndex),
                    sampleIndex,
                    "Sample index must be non-negative.");
            }

            ValidateFinite(distance, nameof(distance));
            ValidateFinite(rollRadians, nameof(rollRadians));
            ValidateFinite(continuousRollRadians, nameof(continuousRollRadians));

            SampleIndex = sampleIndex;
            Distance = distance;
            RollRadians = rollRadians;
            ContinuousRollRadians = continuousRollRadians;
        }

        public int SampleIndex { get; }

        public double Distance { get; }

        public double RollRadians { get; }

        public double ContinuousRollRadians { get; }

        public double RollDegrees => RollRadians * RadiansToDegrees;

        public double ContinuousRollDegrees => ContinuousRollRadians * RadiansToDegrees;

        private static void ValidateFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be finite.");
            }
        }
    }

    public readonly struct ContinuousRollDiagnosticsInterval
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public ContinuousRollDiagnosticsInterval(
            int startSampleIndex,
            int endSampleIndex,
            double startDistance,
            double endDistance,
            double startRollRadians,
            double endRollRadians,
            double startContinuousRollRadians,
            double endContinuousRollRadians,
            double rawRollDeltaRadians,
            double rollDeltaRadians,
            double rollRateRadPerMeter,
            bool usedWrapAround)
        {
            if (startSampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startSampleIndex),
                    startSampleIndex,
                    "Start sample index must be non-negative.");
            }

            if (endSampleIndex <= startSampleIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endSampleIndex),
                    endSampleIndex,
                    "End sample index must be greater than start sample index.");
            }

            ValidateFinite(startDistance, nameof(startDistance));
            ValidateFinite(endDistance, nameof(endDistance));
            ValidateFinite(startRollRadians, nameof(startRollRadians));
            ValidateFinite(endRollRadians, nameof(endRollRadians));
            ValidateFinite(startContinuousRollRadians, nameof(startContinuousRollRadians));
            ValidateFinite(endContinuousRollRadians, nameof(endContinuousRollRadians));
            ValidateFinite(rawRollDeltaRadians, nameof(rawRollDeltaRadians));
            ValidateFinite(rollDeltaRadians, nameof(rollDeltaRadians));
            ValidateFinite(rollRateRadPerMeter, nameof(rollRateRadPerMeter));

            double distanceDelta = endDistance - startDistance;
            if (distanceDelta <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endDistance),
                    endDistance,
                    "End distance must be greater than start distance.");
            }

            StartSampleIndex = startSampleIndex;
            EndSampleIndex = endSampleIndex;
            StartDistance = startDistance;
            EndDistance = endDistance;
            StartRollRadians = startRollRadians;
            EndRollRadians = endRollRadians;
            StartContinuousRollRadians = startContinuousRollRadians;
            EndContinuousRollRadians = endContinuousRollRadians;
            RawRollDeltaRadians = rawRollDeltaRadians;
            RollDeltaRadians = rollDeltaRadians;
            RollRateRadPerMeter = rollRateRadPerMeter;
            UsedWrapAround = usedWrapAround;
        }

        public int StartSampleIndex { get; }

        public int EndSampleIndex { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double DistanceDelta => EndDistance - StartDistance;

        public double StartRollRadians { get; }

        public double EndRollRadians { get; }

        public double StartContinuousRollRadians { get; }

        public double EndContinuousRollRadians { get; }

        public double RawRollDeltaRadians { get; }

        public double RollDeltaRadians { get; }

        public double RollRateRadPerMeter { get; }

        public bool UsedWrapAround { get; }

        public double AbsoluteRollDeltaRadians => SystemMath.Abs(RollDeltaRadians);

        public double AbsoluteRollRateRadPerMeter => SystemMath.Abs(RollRateRadPerMeter);

        public double RawRollDeltaDegrees => RawRollDeltaRadians * RadiansToDegrees;

        public double RollDeltaDegrees => RollDeltaRadians * RadiansToDegrees;

        public double RollRateDegreesPerMeter => RollRateRadPerMeter * RadiansToDegrees;

        public double AbsoluteRollRateDegreesPerMeter => AbsoluteRollRateRadPerMeter * RadiansToDegrees;

        private static void ValidateFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be finite.");
            }
        }
    }

    public readonly struct ContinuousRollWarning
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public ContinuousRollWarning(
            ContinuousRollWarningKind kind,
            ContinuousRollDiagnosticsInterval interval,
            double actualValue,
            double thresholdValue)
        {
            if (double.IsNaN(actualValue) || double.IsInfinity(actualValue) || actualValue < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actualValue),
                    actualValue,
                    "Warning actual value must be finite and non-negative.");
            }

            if (double.IsNaN(thresholdValue) || double.IsInfinity(thresholdValue) || thresholdValue < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(thresholdValue),
                    thresholdValue,
                    "Warning threshold must be finite and non-negative.");
            }

            Kind = kind;
            Interval = interval;
            ActualValue = actualValue;
            ThresholdValue = thresholdValue;
        }

        public ContinuousRollWarningKind Kind { get; }

        public ContinuousRollDiagnosticsInterval Interval { get; }

        public double ActualValue { get; }

        public double ThresholdValue { get; }

        public double ActualValueDegrees => ActualValue * RadiansToDegrees;

        public double ThresholdValueDegrees => ThresholdValue * RadiansToDegrees;
    }

    public readonly struct ContinuousRollDiagnosticsSummary
    {
        private const double RadiansToDegrees = 180.0 / SystemMath.PI;

        public ContinuousRollDiagnosticsSummary(
            int sampleCount,
            int intervalCount,
            int warningCount,
            ContinuousRollWrapMode wrapMode,
            double maxAbsoluteRollDeltaRadians,
            double averageAbsoluteRollDeltaRadians,
            double maxAbsoluteRollRateRadPerMeter,
            double averageAbsoluteRollRateRadPerMeter)
        {
            if (sampleCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount),
                    sampleCount,
                    "Sample count must be non-negative.");
            }

            if (intervalCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalCount),
                    intervalCount,
                    "Interval count must be non-negative.");
            }

            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningCount),
                    warningCount,
                    "Warning count must be non-negative.");
            }

            ValidateFiniteNonNegative(maxAbsoluteRollDeltaRadians, nameof(maxAbsoluteRollDeltaRadians));
            ValidateFiniteNonNegative(averageAbsoluteRollDeltaRadians, nameof(averageAbsoluteRollDeltaRadians));
            ValidateFiniteNonNegative(maxAbsoluteRollRateRadPerMeter, nameof(maxAbsoluteRollRateRadPerMeter));
            ValidateFiniteNonNegative(averageAbsoluteRollRateRadPerMeter, nameof(averageAbsoluteRollRateRadPerMeter));

            SampleCount = sampleCount;
            IntervalCount = intervalCount;
            WarningCount = warningCount;
            WrapMode = wrapMode;
            MaxAbsoluteRollDeltaRadians = maxAbsoluteRollDeltaRadians;
            AverageAbsoluteRollDeltaRadians = averageAbsoluteRollDeltaRadians;
            MaxAbsoluteRollRateRadPerMeter = maxAbsoluteRollRateRadPerMeter;
            AverageAbsoluteRollRateRadPerMeter = averageAbsoluteRollRateRadPerMeter;
        }

        public int SampleCount { get; }

        public int IntervalCount { get; }

        public int WarningCount { get; }

        public ContinuousRollWrapMode WrapMode { get; }

        public double MaxAbsoluteRollDeltaRadians { get; }

        public double AverageAbsoluteRollDeltaRadians { get; }

        public double MaxAbsoluteRollRateRadPerMeter { get; }

        public double AverageAbsoluteRollRateRadPerMeter { get; }

        public double MaxAbsoluteRollDeltaDegrees => MaxAbsoluteRollDeltaRadians * RadiansToDegrees;

        public double AverageAbsoluteRollDeltaDegrees => AverageAbsoluteRollDeltaRadians * RadiansToDegrees;

        public double MaxAbsoluteRollRateDegreesPerMeter => MaxAbsoluteRollRateRadPerMeter * RadiansToDegrees;

        public double AverageAbsoluteRollRateDegreesPerMeter => AverageAbsoluteRollRateRadPerMeter * RadiansToDegrees;

        private static void ValidateFiniteNonNegative(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }
    }

    public sealed class ContinuousRollDiagnosticsReport
    {
        public ContinuousRollDiagnosticsReport(
            IReadOnlyList<ContinuousRollDiagnosticsSample> samples,
            IReadOnlyList<ContinuousRollDiagnosticsInterval> intervals,
            IReadOnlyList<ContinuousRollWarning> warnings,
            ContinuousRollDiagnosticsOptions options,
            ContinuousRollDiagnosticsSummary summary)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            Intervals = intervals ?? throw new ArgumentNullException(nameof(intervals));
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
            Options = options;
            Summary = summary;
        }

        public IReadOnlyList<ContinuousRollDiagnosticsSample> Samples { get; }

        public IReadOnlyList<ContinuousRollDiagnosticsInterval> Intervals { get; }

        public IReadOnlyList<ContinuousRollWarning> Warnings { get; }

        public ContinuousRollDiagnosticsOptions Options { get; }

        public ContinuousRollDiagnosticsSummary Summary { get; }

        public bool HasWarnings => Warnings.Count > 0;

        public string ToDiagnosticString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "Continuous roll diagnostics: samples={0}, intervals={1}, warnings={2}, wrapMode={3}, maxRollDeltaDeg={4:F3}, avgRollDeltaDeg={5:F3}, maxRollRateDegPerM={6:F3}, avgRollRateDegPerM={7:F3}",
                Summary.SampleCount,
                Summary.IntervalCount,
                Summary.WarningCount,
                Summary.WrapMode,
                Summary.MaxAbsoluteRollDeltaDegrees,
                Summary.AverageAbsoluteRollDeltaDegrees,
                Summary.MaxAbsoluteRollRateDegreesPerMeter,
                Summary.AverageAbsoluteRollRateDegreesPerMeter);

            for (int i = 0; i < Warnings.Count; i++)
            {
                ContinuousRollWarning warning = Warnings[i];
                builder.Append('\n');
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}: samples={1}->{2}, distance={3:F6}->{4:F6}, actualDeg={5:F3}, thresholdDeg={6:F3}",
                    warning.Kind,
                    warning.Interval.StartSampleIndex,
                    warning.Interval.EndSampleIndex,
                    warning.Interval.StartDistance,
                    warning.Interval.EndDistance,
                    warning.ActualValueDegrees,
                    warning.ThresholdValueDegrees);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Backend-only diagnostics for inspecting continuous roll samples over station distance.
    /// </summary>
    public static class ContinuousRollDiagnostics
    {
        private const double TwoPi = 2.0 * SystemMath.PI;
        private const double WrapComparisonTolerance = 1e-12;

        public static ContinuousRollDiagnosticsReport AnalyzeRollRadians(
            IReadOnlyList<double> rollRadians)
        {
            return AnalyzeRollRadians(rollRadians, ContinuousRollDiagnosticsOptions.Default);
        }

        public static ContinuousRollDiagnosticsReport AnalyzeRollRadians(
            IReadOnlyList<double> rollRadians,
            ContinuousRollDiagnosticsOptions options)
        {
            if (rollRadians is null)
            {
                throw new ArgumentNullException(nameof(rollRadians));
            }

            return AnalyzeRollRadians(BuildUnitDistances(rollRadians.Count), rollRadians, options);
        }

        public static ContinuousRollDiagnosticsReport AnalyzeRollRadians(
            IReadOnlyList<double> distances,
            IReadOnlyList<double> rollRadians)
        {
            return AnalyzeRollRadians(distances, rollRadians, ContinuousRollDiagnosticsOptions.Default);
        }

        public static ContinuousRollDiagnosticsReport AnalyzeRollRadians(
            IReadOnlyList<double> distances,
            IReadOnlyList<double> rollRadians,
            ContinuousRollDiagnosticsOptions options)
        {
            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            if (rollRadians is null)
            {
                throw new ArgumentNullException(nameof(rollRadians));
            }

            if (distances.Count != rollRadians.Count)
            {
                throw new ArgumentException(
                    "Distance and roll sample counts must match.",
                    nameof(rollRadians));
            }

            ValidateSamples(distances, rollRadians);

            if (rollRadians.Count == 0)
            {
                return BuildEmptyReport(options);
            }

            var samples = new ContinuousRollDiagnosticsSample[rollRadians.Count];
            var intervals = new ContinuousRollDiagnosticsInterval[SystemMath.Max(0, rollRadians.Count - 1)];
            var warnings = new List<ContinuousRollWarning>();

            double previousContinuousRoll = rollRadians[0];
            samples[0] = new ContinuousRollDiagnosticsSample(
                0,
                distances[0],
                rollRadians[0],
                previousContinuousRoll);

            double maxAbsoluteRollDelta = 0.0;
            double sumAbsoluteRollDelta = 0.0;
            double maxAbsoluteRollRate = 0.0;
            double sumAbsoluteRollRate = 0.0;

            for (int i = 1; i < rollRadians.Count; i++)
            {
                double rawDelta = rollRadians[i] - rollRadians[i - 1];
                double rollDelta = AdjustRollDelta(rawDelta, options.WrapMode);
                double continuousRoll = previousContinuousRoll + rollDelta;
                double distanceDelta = distances[i] - distances[i - 1];
                double rollRate = rollDelta / distanceDelta;
                bool usedWrapAround = SystemMath.Abs(rawDelta - rollDelta) > WrapComparisonTolerance;

                samples[i] = new ContinuousRollDiagnosticsSample(
                    i,
                    distances[i],
                    rollRadians[i],
                    continuousRoll);

                ContinuousRollDiagnosticsInterval interval = new ContinuousRollDiagnosticsInterval(
                    startSampleIndex: i - 1,
                    endSampleIndex: i,
                    startDistance: distances[i - 1],
                    endDistance: distances[i],
                    startRollRadians: rollRadians[i - 1],
                    endRollRadians: rollRadians[i],
                    startContinuousRollRadians: previousContinuousRoll,
                    endContinuousRollRadians: continuousRoll,
                    rawRollDeltaRadians: rawDelta,
                    rollDeltaRadians: rollDelta,
                    rollRateRadPerMeter: rollRate,
                    usedWrapAround: usedWrapAround);

                intervals[i - 1] = interval;

                double absoluteRollDelta = interval.AbsoluteRollDeltaRadians;
                double absoluteRollRate = interval.AbsoluteRollRateRadPerMeter;
                maxAbsoluteRollDelta = SystemMath.Max(maxAbsoluteRollDelta, absoluteRollDelta);
                sumAbsoluteRollDelta += absoluteRollDelta;
                maxAbsoluteRollRate = SystemMath.Max(maxAbsoluteRollRate, absoluteRollRate);
                sumAbsoluteRollRate += absoluteRollRate;

                AddWarningIfExceeded(
                    warnings,
                    ContinuousRollWarningKind.RollDelta,
                    interval,
                    absoluteRollDelta,
                    options.RollDeltaWarningThresholdRadians);
                AddWarningIfExceeded(
                    warnings,
                    ContinuousRollWarningKind.RollRate,
                    interval,
                    absoluteRollRate,
                    options.RollRateWarningThresholdRadPerMeter);

                previousContinuousRoll = continuousRoll;
            }

            return new ContinuousRollDiagnosticsReport(
                Array.AsReadOnly(samples),
                Array.AsReadOnly(intervals),
                warnings.AsReadOnly(),
                options,
                new ContinuousRollDiagnosticsSummary(
                    sampleCount: samples.Length,
                    intervalCount: intervals.Length,
                    warningCount: warnings.Count,
                    wrapMode: options.WrapMode,
                    maxAbsoluteRollDeltaRadians: maxAbsoluteRollDelta,
                    averageAbsoluteRollDeltaRadians: intervals.Length == 0 ? 0.0 : sumAbsoluteRollDelta / intervals.Length,
                    maxAbsoluteRollRateRadPerMeter: maxAbsoluteRollRate,
                    averageAbsoluteRollRateRadPerMeter: intervals.Length == 0 ? 0.0 : sumAbsoluteRollRate / intervals.Length));
        }

        public static ContinuousRollDiagnosticsReport AnalyzeBankingProfile(
            BankingProfile profile,
            IReadOnlyList<double> distances)
        {
            return AnalyzeBankingProfile(profile, distances, ContinuousRollDiagnosticsOptions.Default);
        }

        public static ContinuousRollDiagnosticsReport AnalyzeBankingProfile(
            BankingProfile profile,
            IReadOnlyList<double> distances,
            ContinuousRollDiagnosticsOptions options)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            var rollRadians = new double[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                rollRadians[i] = BankingProfileSampler.SampleRollRadians(profile, distances[i]);
            }

            return AnalyzeRollRadians(distances, rollRadians, options);
        }

        private static ContinuousRollDiagnosticsReport BuildEmptyReport(
            ContinuousRollDiagnosticsOptions options)
        {
            return new ContinuousRollDiagnosticsReport(
                Array.Empty<ContinuousRollDiagnosticsSample>(),
                Array.Empty<ContinuousRollDiagnosticsInterval>(),
                Array.Empty<ContinuousRollWarning>(),
                options,
                new ContinuousRollDiagnosticsSummary(
                    sampleCount: 0,
                    intervalCount: 0,
                    warningCount: 0,
                    wrapMode: options.WrapMode,
                    maxAbsoluteRollDeltaRadians: 0.0,
                    averageAbsoluteRollDeltaRadians: 0.0,
                    maxAbsoluteRollRateRadPerMeter: 0.0,
                    averageAbsoluteRollRateRadPerMeter: 0.0));
        }

        private static void AddWarningIfExceeded(
            List<ContinuousRollWarning> warnings,
            ContinuousRollWarningKind kind,
            ContinuousRollDiagnosticsInterval interval,
            double actualValue,
            double thresholdValue)
        {
            if (actualValue > thresholdValue)
            {
                warnings.Add(new ContinuousRollWarning(kind, interval, actualValue, thresholdValue));
            }
        }

        private static double AdjustRollDelta(double rawDeltaRadians, ContinuousRollWrapMode wrapMode)
        {
            switch (wrapMode)
            {
                case ContinuousRollWrapMode.None:
                    return rawDeltaRadians;

                case ContinuousRollWrapMode.FullTurn:
                    return NormalizeToShortestFullTurnDelta(rawDeltaRadians);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(wrapMode),
                        wrapMode,
                        "Unsupported continuous roll wrap mode.");
            }
        }

        private static double NormalizeToShortestFullTurnDelta(double deltaRadians)
        {
            double normalized = deltaRadians % TwoPi;

            if (normalized <= -SystemMath.PI)
            {
                normalized += TwoPi;
            }
            else if (normalized > SystemMath.PI)
            {
                normalized -= TwoPi;
            }

            return normalized;
        }

        private static double[] BuildUnitDistances(int count)
        {
            var distances = new double[count];
            for (int i = 0; i < count; i++)
            {
                distances[i] = i;
            }

            return distances;
        }

        private static void ValidateSamples(
            IReadOnlyList<double> distances,
            IReadOnlyList<double> rollRadians)
        {
            double previousDistance = double.NegativeInfinity;

            for (int i = 0; i < distances.Count; i++)
            {
                double distance = distances[i];
                double roll = rollRadians[i];

                if (!IsFinite(distance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(distances),
                        distance,
                        $"Distance at index {i} must be finite.");
                }

                if (!IsFinite(roll))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rollRadians),
                        roll,
                        $"Roll at index {i} must be finite.");
                }

                if (distance <= previousDistance)
                {
                    throw new ArgumentException(
                        $"Distances must be in strictly increasing station order. Distance at index {i} is not greater than the previous distance.",
                        nameof(distances));
                }

                previousDistance = distance;
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
