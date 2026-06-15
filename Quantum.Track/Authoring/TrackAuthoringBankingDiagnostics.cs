using System;
using System.Collections.Generic;
using SystemMath = System.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Supported authoring-facing banking diagnostic kinds.
    /// </summary>
    public enum TrackAuthoringBankingDiagnosticKind
    {
        StartEndpointMismatch = 0,
        EndEndpointMismatch = 1,
        RollDiscontinuity = 2,
        RollSlope = 3
    }

    /// <summary>
    /// Identifies where a compiled authoring banking profile came from.
    /// </summary>
    public enum TrackAuthoringBankingProfileSourceKind
    {
        ExplicitAuthored = 0,
        SectionRollFallback = 1
    }

    /// <summary>
    /// Thresholds and sampling controls for authoring-facing banking diagnostics.
    /// </summary>
    public readonly struct TrackAuthoringBankingDiagnosticsOptions
    {
        public TrackAuthoringBankingDiagnosticsOptions(
            double endpointDistanceTolerance,
            int samplesPerProfileInterval,
            ContinuousRollDiagnosticsOptions continuousRollOptions)
        {
            EndpointDistanceTolerance = ValidateEndpointDistanceTolerance(
                endpointDistanceTolerance,
                nameof(endpointDistanceTolerance));

            if (samplesPerProfileInterval < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samplesPerProfileInterval),
                    samplesPerProfileInterval,
                    "Samples per profile interval must be at least one.");
            }

            SamplesPerProfileInterval = samplesPerProfileInterval;
            ContinuousRollOptions = ValidateContinuousRollOptions(continuousRollOptions);
        }

        public double EndpointDistanceTolerance { get; }

        public int SamplesPerProfileInterval { get; }

        public ContinuousRollDiagnosticsOptions ContinuousRollOptions { get; }

        public static TrackAuthoringBankingDiagnosticsOptions Default =>
            new TrackAuthoringBankingDiagnosticsOptions(
                endpointDistanceTolerance: 1e-9,
                samplesPerProfileInterval: 8,
                continuousRollOptions: ContinuousRollDiagnosticsOptions.NoWrap);

        private static double ValidateEndpointDistanceTolerance(double value, string paramName)
        {
            if (!AuthoringValidation.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Endpoint distance tolerance must be finite and non-negative.");
            }

            return value;
        }

        private static ContinuousRollDiagnosticsOptions ValidateContinuousRollOptions(
            ContinuousRollDiagnosticsOptions options)
        {
            return new ContinuousRollDiagnosticsOptions(
                options.WrapMode,
                options.RollDeltaWarningThresholdRadians,
                options.RollRateWarningThresholdRadPerMeter);
        }
    }

    /// <summary>
    /// Endpoint coverage comparison for a compiled authoring banking profile.
    /// </summary>
    public readonly struct TrackAuthoringBankingCoverage
    {
        internal TrackAuthoringBankingCoverage(
            double expectedStartDistance,
            double actualStartDistance,
            double expectedEndDistance,
            double actualEndDistance,
            double endpointDistanceTolerance)
        {
            ExpectedStartDistance = expectedStartDistance;
            ActualStartDistance = actualStartDistance;
            ExpectedEndDistance = expectedEndDistance;
            ActualEndDistance = actualEndDistance;
            EndpointDistanceTolerance = endpointDistanceTolerance;
        }

        public double ExpectedStartDistance { get; }

        public double ActualStartDistance { get; }

        public double StartDistanceDelta => ActualStartDistance - ExpectedStartDistance;

        public double AbsoluteStartDistanceDelta => SystemMath.Abs(StartDistanceDelta);

        public double ExpectedEndDistance { get; }

        public double ActualEndDistance { get; }

        public double EndDistanceDelta => ActualEndDistance - ExpectedEndDistance;

        public double AbsoluteEndDistanceDelta => SystemMath.Abs(EndDistanceDelta);

        public double TotalLength => ExpectedEndDistance;

        public double EndpointDistanceTolerance { get; }

        public bool StartsAtTrackStart => AbsoluteStartDistanceDelta <= EndpointDistanceTolerance;

        public bool EndsAtTrackEnd => AbsoluteEndDistanceDelta <= EndpointDistanceTolerance;

        public bool Passes => StartsAtTrackStart && EndsAtTrackEnd;
    }

    /// <summary>
    /// One authoring-facing banking diagnostic emitted from coverage or sampled roll analysis.
    /// </summary>
    public readonly struct TrackAuthoringBankingDiagnostic
    {
        internal TrackAuthoringBankingDiagnostic(
            TrackAuthoringBankingDiagnosticKind kind,
            double measuredValue,
            double tolerance,
            int? startSampleIndex,
            int? endSampleIndex,
            double? startDistance,
            double? endDistance,
            double? startRollRadians,
            double? endRollRadians,
            double? rollDeltaRadians,
            double? rollRateRadPerMeter,
            double? expectedDistance,
            double? actualDistance,
            double? distanceDelta)
        {
            Kind = kind;
            MeasuredValue = measuredValue;
            Tolerance = tolerance;
            StartSampleIndex = startSampleIndex;
            EndSampleIndex = endSampleIndex;
            StartDistance = startDistance;
            EndDistance = endDistance;
            StartRollRadians = startRollRadians;
            EndRollRadians = endRollRadians;
            RollDeltaRadians = rollDeltaRadians;
            RollRateRadPerMeter = rollRateRadPerMeter;
            ExpectedDistance = expectedDistance;
            ActualDistance = actualDistance;
            DistanceDelta = distanceDelta;
        }

        public TrackAuthoringBankingDiagnosticKind Kind { get; }

        /// <summary>
        /// Non-negative value compared with <see cref="Tolerance"/>.
        /// </summary>
        public double MeasuredValue { get; }

        public double Tolerance { get; }

        public int? StartSampleIndex { get; }

        public int? EndSampleIndex { get; }

        public double? StartDistance { get; }

        public double? EndDistance { get; }

        public double? StartRollRadians { get; }

        public double? EndRollRadians { get; }

        public double? RollDeltaRadians { get; }

        public double? RollRateRadPerMeter { get; }

        public double? ExpectedDistance { get; }

        public double? ActualDistance { get; }

        public double? DistanceDelta { get; }
    }

    /// <summary>
    /// Read-only authoring-facing banking diagnostics report.
    /// </summary>
    public sealed class TrackAuthoringBankingDiagnosticsReport
    {
        private readonly IReadOnlyList<double> _sampleDistances;
        private readonly IReadOnlyList<TrackAuthoringBankingDiagnostic> _diagnostics;

        internal TrackAuthoringBankingDiagnosticsReport(
            TrackAuthoringBankingProfileSourceKind sourceKind,
            TrackAuthoringBankingCoverage coverage,
            IReadOnlyList<double> sampleDistances,
            BankingProfileDiagnosticsReport bankingProfileReport,
            ContinuousRollDiagnosticsReport continuousRollReport,
            IEnumerable<TrackAuthoringBankingDiagnostic> diagnostics,
            TrackAuthoringBankingDiagnosticsOptions options)
        {
            if (sampleDistances is null)
            {
                throw new ArgumentNullException(nameof(sampleDistances));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            SourceKind = sourceKind;
            Coverage = coverage;
            _sampleDistances = new List<double>(sampleDistances).AsReadOnly();
            BankingProfileReport = bankingProfileReport ??
                throw new ArgumentNullException(nameof(bankingProfileReport));
            ContinuousRollReport = continuousRollReport ??
                throw new ArgumentNullException(nameof(continuousRollReport));
            _diagnostics = new List<TrackAuthoringBankingDiagnostic>(diagnostics).AsReadOnly();
            Options = options;
        }

        public TrackAuthoringBankingProfileSourceKind SourceKind { get; }

        public TrackAuthoringBankingCoverage Coverage { get; }

        public IReadOnlyList<double> SampleDistances => _sampleDistances;

        public BankingProfileDiagnosticsReport BankingProfileReport { get; }

        public ContinuousRollDiagnosticsReport ContinuousRollReport { get; }

        public IReadOnlyList<BankingProfileDiagnosticsSample> Samples => BankingProfileReport.Samples;

        public BankingProfileDiagnosticsSummary Summary => BankingProfileReport.Summary;

        public IReadOnlyList<ContinuousRollDiagnosticsSample> ContinuousRollSamples =>
            ContinuousRollReport.Samples;

        public IReadOnlyList<ContinuousRollDiagnosticsInterval> ContinuousRollIntervals =>
            ContinuousRollReport.Intervals;

        public IReadOnlyList<ContinuousRollWarning> ContinuousRollWarnings =>
            ContinuousRollReport.Warnings;

        public IReadOnlyList<TrackAuthoringBankingDiagnostic> Diagnostics => _diagnostics;

        public TrackAuthoringBankingDiagnosticsOptions Options { get; }

        public int DiagnosticCount => Diagnostics.Count;

        public bool HasDiagnostics => Diagnostics.Count > 0;
    }

    /// <summary>
    /// Authoring-facing banking diagnostics over a compiled authoring BankingProfile.
    /// </summary>
    public static class TrackAuthoringBankingDiagnostics
    {
        public static TrackAuthoringBankingDiagnosticsReport Analyze(
            TrackAuthoringDefinition definition)
        {
            return Analyze(definition, TrackAuthoringBankingDiagnosticsOptions.Default);
        }

        public static TrackAuthoringBankingDiagnosticsReport Analyze(
            TrackAuthoringDefinition definition,
            TrackAuthoringBankingDiagnosticsOptions options)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ValidateOptions(options);
            TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(definition);
            return AnalyzeCompilation(compilation, options);
        }

        public static TrackAuthoringBankingDiagnosticsReport Analyze(
            TrackAuthoringCompilation compilation)
        {
            return Analyze(compilation, TrackAuthoringBankingDiagnosticsOptions.Default);
        }

        public static TrackAuthoringBankingDiagnosticsReport Analyze(
            TrackAuthoringCompilation compilation,
            TrackAuthoringBankingDiagnosticsOptions options)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            ValidateOptions(options);
            return AnalyzeCompilation(compilation, options);
        }

        private static TrackAuthoringBankingDiagnosticsReport AnalyzeCompilation(
            TrackAuthoringCompilation compilation,
            TrackAuthoringBankingDiagnosticsOptions options)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            IReadOnlyList<double> sampleDistances = BuildSampleDistances(
                compilation,
                options.SamplesPerProfileInterval);
            BankingProfileDiagnosticsReport bankingProfileReport =
                BankingProfileDiagnostics.Sample(compilation.BankingProfile, sampleDistances);
            double[] rollRadians = GetRollRadians(bankingProfileReport.Samples);
            ContinuousRollDiagnosticsReport continuousRollReport =
                ContinuousRollDiagnostics.AnalyzeRollRadians(
                    sampleDistances,
                    rollRadians,
                    options.ContinuousRollOptions);
            TrackAuthoringBankingCoverage coverage = AnalyzeCoverage(
                compilation,
                options.EndpointDistanceTolerance);
            var diagnostics = new List<TrackAuthoringBankingDiagnostic>();
            AddCoverageDiagnostics(diagnostics, coverage);
            AddContinuousRollDiagnostics(diagnostics, continuousRollReport.Warnings);

            return new TrackAuthoringBankingDiagnosticsReport(
                DetermineSourceKind(compilation),
                coverage,
                sampleDistances,
                bankingProfileReport,
                continuousRollReport,
                diagnostics,
                options);
        }

        private static void ValidateOptions(TrackAuthoringBankingDiagnosticsOptions options)
        {
            _ = new TrackAuthoringBankingDiagnosticsOptions(
                options.EndpointDistanceTolerance,
                options.SamplesPerProfileInterval,
                options.ContinuousRollOptions);
        }

        private static TrackAuthoringBankingProfileSourceKind DetermineSourceKind(
            TrackAuthoringCompilation compilation)
        {
            return compilation.Definition.Banking != null
                ? TrackAuthoringBankingProfileSourceKind.ExplicitAuthored
                : TrackAuthoringBankingProfileSourceKind.SectionRollFallback;
        }

        private static IReadOnlyList<double> BuildSampleDistances(
            TrackAuthoringCompilation compilation,
            int samplesPerProfileInterval)
        {
            var distances = new SortedSet<double>
            {
                0.0,
                compilation.TotalLength
            };
            IReadOnlyList<BankingProfileKey> keys = compilation.BankingProfile.Keys;

            for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                distances.Add(keys[keyIndex].Distance);
            }

            for (int keyIndex = 0; keyIndex < keys.Count - 1; keyIndex++)
            {
                double startDistance = keys[keyIndex].Distance;
                double endDistance = keys[keyIndex + 1].Distance;
                double intervalLength = endDistance - startDistance;

                for (int sample = 1; sample < samplesPerProfileInterval; sample++)
                {
                    distances.Add(
                        startDistance +
                        (intervalLength * sample / samplesPerProfileInterval));
                }
            }

            return new List<double>(distances).AsReadOnly();
        }

        private static double[] GetRollRadians(
            IReadOnlyList<BankingProfileDiagnosticsSample> samples)
        {
            var rollRadians = new double[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                rollRadians[i] = samples[i].RollRadians;
            }

            return rollRadians;
        }

        private static TrackAuthoringBankingCoverage AnalyzeCoverage(
            TrackAuthoringCompilation compilation,
            double endpointDistanceTolerance)
        {
            IReadOnlyList<BankingProfileKey> keys = compilation.BankingProfile.Keys;
            return new TrackAuthoringBankingCoverage(
                expectedStartDistance: 0.0,
                actualStartDistance: keys[0].Distance,
                expectedEndDistance: compilation.TotalLength,
                actualEndDistance: keys[keys.Count - 1].Distance,
                endpointDistanceTolerance: endpointDistanceTolerance);
        }

        private static void AddCoverageDiagnostics(
            ICollection<TrackAuthoringBankingDiagnostic> diagnostics,
            TrackAuthoringBankingCoverage coverage)
        {
            if (!coverage.StartsAtTrackStart)
            {
                diagnostics.Add(CreateEndpointDiagnostic(
                    TrackAuthoringBankingDiagnosticKind.StartEndpointMismatch,
                    coverage.ExpectedStartDistance,
                    coverage.ActualStartDistance,
                    coverage.StartDistanceDelta,
                    coverage.EndpointDistanceTolerance));
            }

            if (!coverage.EndsAtTrackEnd)
            {
                diagnostics.Add(CreateEndpointDiagnostic(
                    TrackAuthoringBankingDiagnosticKind.EndEndpointMismatch,
                    coverage.ExpectedEndDistance,
                    coverage.ActualEndDistance,
                    coverage.EndDistanceDelta,
                    coverage.EndpointDistanceTolerance));
            }
        }

        private static TrackAuthoringBankingDiagnostic CreateEndpointDiagnostic(
            TrackAuthoringBankingDiagnosticKind kind,
            double expectedDistance,
            double actualDistance,
            double distanceDelta,
            double tolerance)
        {
            return new TrackAuthoringBankingDiagnostic(
                kind,
                SystemMath.Abs(distanceDelta),
                tolerance,
                startSampleIndex: null,
                endSampleIndex: null,
                startDistance: null,
                endDistance: null,
                startRollRadians: null,
                endRollRadians: null,
                rollDeltaRadians: null,
                rollRateRadPerMeter: null,
                expectedDistance: expectedDistance,
                actualDistance: actualDistance,
                distanceDelta: distanceDelta);
        }

        private static void AddContinuousRollDiagnostics(
            ICollection<TrackAuthoringBankingDiagnostic> diagnostics,
            IReadOnlyList<ContinuousRollWarning> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                ContinuousRollWarning warning = warnings[i];
                diagnostics.Add(CreateContinuousRollDiagnostic(warning));
            }
        }

        private static TrackAuthoringBankingDiagnostic CreateContinuousRollDiagnostic(
            ContinuousRollWarning warning)
        {
            ContinuousRollDiagnosticsInterval interval = warning.Interval;
            return new TrackAuthoringBankingDiagnostic(
                MapWarningKind(warning.Kind),
                warning.ActualValue,
                warning.ThresholdValue,
                interval.StartSampleIndex,
                interval.EndSampleIndex,
                interval.StartDistance,
                interval.EndDistance,
                interval.StartRollRadians,
                interval.EndRollRadians,
                interval.RollDeltaRadians,
                interval.RollRateRadPerMeter,
                expectedDistance: null,
                actualDistance: null,
                distanceDelta: null);
        }

        private static TrackAuthoringBankingDiagnosticKind MapWarningKind(
            ContinuousRollWarningKind kind)
        {
            switch (kind)
            {
                case ContinuousRollWarningKind.RollDelta:
                    return TrackAuthoringBankingDiagnosticKind.RollDiscontinuity;

                case ContinuousRollWarningKind.RollRate:
                    return TrackAuthoringBankingDiagnosticKind.RollSlope;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unsupported continuous roll warning kind.");
            }
        }
    }
}
