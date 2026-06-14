using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track.Internal
{
    internal sealed class CompiledTrackSamplingContext
    {
        private const int ArcLengthSamples = 100;
        private const int TransportSamplesPerSegment = 100;
        private const double DeclaredLengthAbsoluteTolerance = 1e-3;
        private const double DeclaredLengthRelativeTolerance = 1e-6;

        private readonly CompiledTrackSegment[] _segments;
        private readonly Lazy<CanonicalTransportedFrameSampler>? _canonicalFrameSampler;
        private readonly Vector3d? _authoredStartNormal;

        private CompiledTrackSamplingContext(
            CompiledTrackSegment[] segments,
            double totalLength,
            IReadOnlyList<double> transportNodeDistances,
            Vector3d? authoredStartNormal)
        {
            _segments = segments;
            TotalLength = totalLength;
            _authoredStartNormal = authoredStartNormal;
            if (segments.Length > 0)
            {
                _canonicalFrameSampler = new Lazy<CanonicalTransportedFrameSampler>(
                    () => new CanonicalTransportedFrameSampler(
                        this,
                        transportNodeDistances,
                        _authoredStartNormal),
                    isThreadSafe: true);
            }
        }

        public double TotalLength { get; }

        public static CompiledTrackSamplingContext Compile(TrackDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            int segmentCount = document.Segments.Count;
            var segments = new CompiledTrackSegment[segmentCount];
            double totalLength = 0.0;

            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i];

                if (segment is null)
                {
                    throw new InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                ValidateFinitePositiveLength(segment, i);
                ValidateFiniteRoll(segment, i);

                ArcLengthLUT? arcLengthLookup = null;
                double geometricLength = segment.Length;

                if (segment.Spline is IParamCurve spline)
                {
                    arcLengthLookup = BuildArcLengthLookup(spline, i);
                    ValidateMeasuredLength(arcLengthLookup.TotalLength, i);
                    if (segment.Spline is IArcLengthCurve arcLengthCurve)
                    {
                        geometricLength = arcLengthCurve.Length;
                        ValidateMeasuredLength(geometricLength, i);
                        ValidateLengthsMatch(
                            geometricLength,
                            arcLengthLookup.TotalLength,
                            i,
                            "reported arc length");
                    }
                    else
                    {
                        geometricLength = arcLengthLookup.TotalLength;
                    }

                    ValidateMeasuredLength(geometricLength, i);
                    ValidateDeclaredLengthMatchesMeasured(segment.Length, geometricLength, i);
                    ValidateSplineTangents(spline, i);
                }

                segments[i] = new CompiledTrackSegment(
                    segment,
                    totalLength,
                    geometricLength,
                    arcLengthLookup,
                    arcLengthLookup?.TotalLength ?? geometricLength);
                totalLength += geometricLength;
            }

            return new CompiledTrackSamplingContext(
                segments,
                totalLength,
                BuildTransportNodeDistances(segments, totalLength),
                document.StartPose?.Normal);
        }

        public static CompiledTrackSamplingContext Compile(
            TrackDocument document,
            TrackSamplingOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            int segmentCount = document.Segments.Count;
            var segments = new CompiledTrackSegment[segmentCount];
            double totalLength = 0.0;

            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i];

                if (segment is null)
                {
                    throw new InvalidOperationException("TrackDocument contains a null segment entry.");
                }

                ValidateFinitePositiveLength(segment, i);
                ValidateFiniteRoll(segment, i);

                ArcLengthLUT? arcLengthLookup = null;
                double geometricLength = segment.Length;

                if (segment.Spline is IParamCurve spline)
                {
                    arcLengthLookup = BuildArcLengthLookup(spline, i, options);
                    ValidateMeasuredLength(arcLengthLookup.TotalLength, i);
                    if (segment.Spline is IArcLengthCurve arcLengthCurve)
                    {
                        geometricLength = arcLengthCurve.Length;
                        ValidateMeasuredLength(geometricLength, i);
                        ValidateLengthsMatch(
                            geometricLength,
                            arcLengthLookup.TotalLength,
                            i,
                            "reported arc length");
                    }
                    else
                    {
                        geometricLength = arcLengthLookup.TotalLength;
                    }

                    ValidateMeasuredLength(geometricLength, i);
                    ValidateDeclaredLengthMatchesMeasured(segment.Length, geometricLength, i);
                    ValidateSplineTangents(spline, i, options.ArcLengthSamples);
                }

                segments[i] = new CompiledTrackSegment(
                    segment,
                    totalLength,
                    geometricLength,
                    arcLengthLookup,
                    arcLengthLookup?.TotalLength ?? geometricLength);
                totalLength += geometricLength;
            }

            return new CompiledTrackSamplingContext(
                segments,
                totalLength,
                BuildTransportNodeDistances(
                    segments,
                    totalLength,
                    options.TransportSamplesPerSegment),
                document.StartPose?.Normal);
        }

        public static CompiledTrackSamplingContext? TryCompile(
            TrackDocument document,
            TrackSamplingOptions options,
            ICollection<TrackRuntimeDiagnostic> diagnostics,
            out TrackRuntimeDiagnostic? failureDiagnostic)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            failureDiagnostic = null;
            int segmentCount = document.Segments.Count;
            if (segmentCount == 0)
            {
                diagnostics.Add(new TrackRuntimeDiagnostic(
                    TrackRuntimeDiagnosticCode.EmptyTrack,
                    TrackRuntimeDiagnosticSeverity.Warning,
                    "TrackDocument contains no segments."));
            }

            long transportNodeCount = ((long)segmentCount * options.TransportSamplesPerSegment) + 1L;
            if (segmentCount > 0 && transportNodeCount > int.MaxValue)
            {
                failureDiagnostic = new TrackRuntimeDiagnostic(
                    TrackRuntimeDiagnosticCode.SamplingCapacityExceeded,
                    TrackRuntimeDiagnosticSeverity.Error,
                    "Track transport sampling exceeds the supported node capacity.");
                diagnostics.Add(failureDiagnostic);
                return null;
            }

            var segments = new CompiledTrackSegment[segmentCount];
            double totalLength = 0.0;

            for (int i = 0; i < segmentCount; i++)
            {
                TrackSegment segment = document.Segments[i];
                if (segment is null)
                {
                    return Fail(
                        TrackRuntimeDiagnosticCode.NullSegment,
                        "TrackDocument contains a null segment entry.",
                        diagnostics,
                        out failureDiagnostic,
                        i);
                }

                if (!IsFinite(segment.Length) || segment.Length <= 0.0)
                {
                    return Fail(
                        TrackRuntimeDiagnosticCode.InvalidDeclaredLength,
                        $"Track segment at index {i} must have a finite declared length greater than zero.",
                        diagnostics,
                        out failureDiagnostic,
                        i,
                        segment.Id);
                }

                if (!IsFinite(segment.RollRadians))
                {
                    return Fail(
                        TrackRuntimeDiagnosticCode.InvalidRoll,
                        $"Track segment at index {i} must have a finite roll angle.",
                        diagnostics,
                        out failureDiagnostic,
                        i,
                        segment.Id);
                }

                ArcLengthLUT? arcLengthLookup = null;
                double geometricLength = segment.Length;

                if (segment.Spline is IParamCurve spline)
                {
                    try
                    {
                        arcLengthLookup = new ArcLengthLUT(
                            spline,
                            options.ArcLengthSamples,
                            options.ArcLengthTolerance);
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException))
                    {
                        return Fail(
                            TrackRuntimeDiagnosticCode.SplineMeasurementFailed,
                            $"Track segment at index {i} spline could not be measured: {ex.Message}",
                            diagnostics,
                            out failureDiagnostic,
                            i,
                            segment.Id);
                    }

                    if (!IsFinite(arcLengthLookup.TotalLength) ||
                        arcLengthLookup.TotalLength <= MathUtil.Epsilon)
                    {
                        return Fail(
                            TrackRuntimeDiagnosticCode.InvalidMeasuredLength,
                            $"Track segment at index {i} spline must have a finite geometric length greater than zero.",
                            diagnostics,
                            out failureDiagnostic,
                            i,
                            segment.Id);
                    }

                    if (segment.Spline is IArcLengthCurve arcLengthCurve)
                    {
                        try
                        {
                            geometricLength = arcLengthCurve.Length;
                        }
                        catch (Exception ex) when (!(ex is OutOfMemoryException))
                        {
                            return Fail(
                                TrackRuntimeDiagnosticCode.SplineMeasurementFailed,
                                $"Track segment at index {i} reported arc length could not be read: {ex.Message}",
                                diagnostics,
                                out failureDiagnostic,
                                i,
                                segment.Id);
                        }

                        if (!IsFinite(geometricLength) || geometricLength <= MathUtil.Epsilon)
                        {
                            return Fail(
                                TrackRuntimeDiagnosticCode.InvalidMeasuredLength,
                                $"Track segment at index {i} spline must have a finite geometric length greater than zero.",
                                diagnostics,
                                out failureDiagnostic,
                                i,
                                segment.Id);
                        }

                        if (!LengthsMatch(geometricLength, arcLengthLookup.TotalLength))
                        {
                            return Fail(
                                TrackRuntimeDiagnosticCode.ReportedArcLengthMismatch,
                                BuildLengthMismatchMessage(
                                    geometricLength,
                                    arcLengthLookup.TotalLength,
                                    i,
                                    "reported arc length"),
                                diagnostics,
                                out failureDiagnostic,
                                i,
                                segment.Id);
                        }
                    }
                    else
                    {
                        geometricLength = arcLengthLookup.TotalLength;
                    }

                    if (!LengthsMatch(segment.Length, geometricLength))
                    {
                        return Fail(
                            TrackRuntimeDiagnosticCode.DeclaredLengthMismatch,
                            BuildLengthMismatchMessage(
                                segment.Length,
                                geometricLength,
                                i,
                                "declared length"),
                            diagnostics,
                            out failureDiagnostic,
                            i,
                            segment.Id);
                    }

                    for (int sampleIndex = 0; sampleIndex <= options.ArcLengthSamples; sampleIndex++)
                    {
                        double t = (double)sampleIndex / options.ArcLengthSamples;
                        Vector3d tangent;

                        try
                        {
                            tangent = spline.Tangent(t);
                        }
                        catch (Exception ex) when (!(ex is OutOfMemoryException))
                        {
                            return Fail(
                                TrackRuntimeDiagnosticCode.SplineTangentEvaluationFailed,
                                $"Track segment at index {i} tangent could not be evaluated at t={t:R}: {ex.Message}",
                                diagnostics,
                                out failureDiagnostic,
                                i,
                                segment.Id,
                                t);
                        }

                        if (!IsFinite(tangent) || tangent.Length <= MathUtil.Epsilon)
                        {
                            return Fail(
                                TrackRuntimeDiagnosticCode.InvalidSplineTangent,
                                $"Track segment at index {i} has an invalid tangent at t={t:R}.",
                                diagnostics,
                                out failureDiagnostic,
                                i,
                                segment.Id,
                                t);
                        }
                    }
                }

                segments[i] = new CompiledTrackSegment(
                    segment,
                    totalLength,
                    geometricLength,
                    arcLengthLookup,
                    arcLengthLookup?.TotalLength ?? geometricLength);
                totalLength += geometricLength;
            }

            return new CompiledTrackSamplingContext(
                segments,
                totalLength,
                BuildTransportNodeDistances(
                    segments,
                    totalLength,
                    options.TransportSamplesPerSegment),
                document.StartPose?.Normal);
        }

        public TrackFrame SampleCanonicalFrame(
            double distance,
            Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (_canonicalFrameSampler is null)
            {
                throw new InvalidOperationException("TrackDocument could not be evaluated because it contains no segments.");
            }

            return _canonicalFrameSampler.Value.Sample(distance, rollRadiansResolver);
        }

        public TrackFrame[] SampleCanonicalFrames(
            IReadOnlyList<double> distances,
            Func<ResolvedTrackDistance, double> rollRadiansResolver)
        {
            if (_canonicalFrameSampler is null)
            {
                throw new InvalidOperationException("TrackDocument could not be evaluated because it contains no segments.");
            }

            return _canonicalFrameSampler.Value.SampleMany(distances, rollRadiansResolver);
        }

        public ResolvedTrackDistance Resolve(double distance)
        {
            double clampedDistance = System.Math.Max(0.0, System.Math.Min(distance, TotalLength));

            for (int i = 0; i < _segments.Length; i++)
            {
                CompiledTrackSegment segment = _segments[i];
                double segmentEndDistance = segment.StationStartDistance + segment.GeometricLength;
                bool isLastSegment = i == _segments.Length - 1;

                if (clampedDistance < segmentEndDistance || isLastSegment)
                {
                    double localDistance = System.Math.Max(
                        0.0,
                        System.Math.Min(
                            clampedDistance - segment.StationStartDistance,
                            segment.GeometricLength));
                    double localT = segment.MapLocalDistanceToParameter(localDistance);

                    return new ResolvedTrackDistance(
                        segment.Segment,
                        localT,
                        localDistance,
                        clampedDistance);
                }
            }

            throw new InvalidOperationException("TrackDocument could not be evaluated at the specified distance.");
        }

        private static double[] BuildTransportNodeDistances(
            IReadOnlyList<CompiledTrackSegment> segments,
            double totalLength)
        {
            return BuildTransportNodeDistances(
                segments,
                totalLength,
                TransportSamplesPerSegment);
        }

        private static double[] BuildTransportNodeDistances(
            IReadOnlyList<CompiledTrackSegment> segments,
            double totalLength,
            int samplesPerSegment)
        {
            if (segments.Count == 0)
            {
                return Array.Empty<double>();
            }

            var distances = new double[(segments.Count * samplesPerSegment) + 1];
            distances[0] = 0.0;
            int nodeIndex = 1;

            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                CompiledTrackSegment segment = segments[segmentIndex];
                for (int sampleIndex = 1; sampleIndex <= samplesPerSegment; sampleIndex++)
                {
                    double fraction = (double)sampleIndex / samplesPerSegment;
                    distances[nodeIndex++] = segment.StationStartDistance + (segment.GeometricLength * fraction);
                }
            }

            distances[distances.Length - 1] = totalLength;
            return distances;
        }

        private static void ValidateFinitePositiveLength(TrackSegment segment, int segmentIndex)
        {
            if (!IsFinite(segment.Length) || segment.Length <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} must have a finite declared length greater than zero.");
            }
        }

        private static void ValidateFiniteRoll(TrackSegment segment, int segmentIndex)
        {
            if (!IsFinite(segment.RollRadians))
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} must have a finite roll angle.");
            }
        }

        private static ArcLengthLUT BuildArcLengthLookup(IParamCurve spline, int segmentIndex)
        {
            return BuildArcLengthLookup(
                spline,
                segmentIndex,
                TrackSamplingOptions.Default);
        }

        private static ArcLengthLUT BuildArcLengthLookup(
            IParamCurve spline,
            int segmentIndex,
            TrackSamplingOptions options)
        {
            try
            {
                return new ArcLengthLUT(
                    spline,
                    samples: options.ArcLengthSamples,
                    tolerance: options.ArcLengthTolerance);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} spline could not be measured.",
                    ex);
            }
        }

        private static void ValidateMeasuredLength(double measuredLength, int segmentIndex)
        {
            if (!IsFinite(measuredLength) || measuredLength <= MathUtil.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Track segment at index {segmentIndex} spline must have a finite geometric length greater than zero.");
            }
        }

        private static void ValidateDeclaredLengthMatchesMeasured(
            double declaredLength,
            double measuredLength,
            int segmentIndex)
        {
            ValidateLengthsMatch(declaredLength, measuredLength, segmentIndex, "declared length");
        }

        private static void ValidateLengthsMatch(
            double statedLength,
            double measuredLength,
            int segmentIndex,
            string statedLengthLabel)
        {
            double tolerance = GetLengthTolerance(measuredLength);

            if (System.Math.Abs(statedLength - measuredLength) > tolerance)
            {
                throw new InvalidOperationException(BuildLengthMismatchMessage(
                    statedLength,
                    measuredLength,
                    segmentIndex,
                    statedLengthLabel));
            }
        }

        private static void ValidateSplineTangents(IParamCurve spline, int segmentIndex)
        {
            ValidateSplineTangents(spline, segmentIndex, ArcLengthSamples);
        }

        private static void ValidateSplineTangents(
            IParamCurve spline,
            int segmentIndex,
            int sampleCount)
        {
            for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
            {
                double t = (double)sampleIndex / sampleCount;
                Vector3d tangent;

                try
                {
                    tangent = spline.Tangent(t);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    throw new InvalidOperationException(
                        $"Track segment at index {segmentIndex} has an invalid tangent at t={t:R}.",
                        ex);
                }

                if (!IsFinite(tangent) || tangent.Length <= MathUtil.Epsilon)
                {
                    throw new InvalidOperationException(
                        $"Track segment at index {segmentIndex} has an invalid tangent at t={t:R}.");
                }
            }
        }

        private static CompiledTrackSamplingContext? Fail(
            TrackRuntimeDiagnosticCode code,
            string message,
            ICollection<TrackRuntimeDiagnostic> diagnostics,
            out TrackRuntimeDiagnostic? failureDiagnostic,
            int? segmentIndex = null,
            string? segmentId = null,
            double? splineParameter = null)
        {
            failureDiagnostic = new TrackRuntimeDiagnostic(
                code,
                TrackRuntimeDiagnosticSeverity.Error,
                message,
                segmentIndex,
                segmentId,
                splineParameter);
            diagnostics.Add(failureDiagnostic);
            return null;
        }

        private static bool LengthsMatch(double statedLength, double measuredLength)
        {
            return System.Math.Abs(statedLength - measuredLength) <= GetLengthTolerance(measuredLength);
        }

        private static double GetLengthTolerance(double measuredLength)
        {
            return System.Math.Max(
                DeclaredLengthAbsoluteTolerance,
                measuredLength * DeclaredLengthRelativeTolerance);
        }

        private static string BuildLengthMismatchMessage(
            double statedLength,
            double measuredLength,
            int segmentIndex,
            string statedLengthLabel)
        {
            double tolerance = GetLengthTolerance(measuredLength);
            return $"Track segment at index {segmentIndex} {statedLengthLabel} {statedLength:R} does not match measured geometric length {measuredLength:R} within tolerance {tolerance:R}.";
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(Vector3d value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }
    }
}
