using System;
using System.Collections.Generic;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Builds canonical engineering snapshots exclusively from an existing authoring compilation.
    /// </summary>
    public static class EngineeringSnapshotBuilder
    {
        public static EngineeringSnapshot Build(
            TrackAuthoringCompilation compilation,
            EngineeringSnapshotRequest request)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            double[] stationGrid = BuildUniformStationGrid(
                compilation.TotalLength,
                request.StationSampleCount);
            var evaluator = new TrackEvaluator(compilation.Runtime);
            TrackFrame[] frames = BankingProfileSampler.SampleFramesAtDistances(
                evaluator,
                compilation.BankingProfile,
                stationGrid);

            var geometry = new EngineeringGeometrySample[stationGrid.Length];
            var bankingRollRadians = new double[stationGrid.Length];
            for (int sampleIndex = 0; sampleIndex < stationGrid.Length; sampleIndex++)
            {
                double station = stationGrid[sampleIndex];
                TrackFrame frame = frames[sampleIndex];
                double? curvatureMagnitude = evaluator.TryGetCurvatureAtDistance(
                    station,
                    out double curvature)
                    ? curvature
                    : (double?)null;

                geometry[sampleIndex] = new EngineeringGeometrySample(
                    sampleIndex,
                    station,
                    frame.Position,
                    frame.Tangent,
                    curvatureMagnitude);
                bankingRollRadians[sampleIndex] = BankingProfileSampler.SampleRollRadians(
                    compilation.BankingProfile,
                    station);
            }

            EngineeringResolvedSectionMetadata[] resolvedSections =
                BuildResolvedSections(compilation.ResolvedSections);
            EngineeringSectionBoundaryMetadata[] boundaries =
                BuildSectionBoundaries(resolvedSections);
            EngineeringControlPointMetadata[] controlPoints =
                BuildControlPointMetadata(compilation.Definition.Sections);
            EngineeringProfileKeyMetadata[] profileKeys = BuildProfileKeyMetadata(
                compilation.BankingProfile.Keys,
                compilation.Definition.Banking != null);
            TrackSamplingOptions runtimeSampling = compilation.Runtime.SamplingOptions;

            return new EngineeringSnapshot(
                new EngineeringSnapshotRevisionMetadata(
                    request.CompilationRevision,
                    request.SnapshotRevision),
                new EngineeringSnapshotSamplingMetadata(
                    stationGrid.Length,
                    runtimeSampling.ArcLengthSampleCount,
                    runtimeSampling.ArcLengthTolerance,
                    runtimeSampling.TransportSamplesPerSegment),
                compilation.TotalLength,
                stationGrid,
                geometry,
                frames,
                bankingRollRadians,
                resolvedSections,
                boundaries,
                controlPoints,
                profileKeys);
        }

        private static double[] BuildUniformStationGrid(double totalLength, int sampleCount)
        {
            var stations = new double[sampleCount];
            double denominator = sampleCount - 1;
            for (int sampleIndex = 0; sampleIndex < sampleCount - 1; sampleIndex++)
            {
                stations[sampleIndex] = totalLength * sampleIndex / denominator;
            }

            stations[sampleCount - 1] = totalLength;
            return stations;
        }

        private static EngineeringResolvedSectionMetadata[] BuildResolvedSections(
            IReadOnlyList<ResolvedSectionInterval<GeometricSectionDefinition>> source)
        {
            var result = new EngineeringResolvedSectionMetadata[source.Count];
            for (int sectionIndex = 0; sectionIndex < source.Count; sectionIndex++)
            {
                ResolvedSectionInterval<GeometricSectionDefinition> interval = source[sectionIndex];
                result[sectionIndex] = new EngineeringResolvedSectionMetadata(
                    sectionIndex,
                    interval.Section.Id,
                    interval.StartDistance,
                    interval.EndDistance,
                    interval.IncludeEndDistance);
            }

            return result;
        }

        private static EngineeringSectionBoundaryMetadata[] BuildSectionBoundaries(
            IReadOnlyList<EngineeringResolvedSectionMetadata> sections)
        {
            var result = new EngineeringSectionBoundaryMetadata[
                System.Math.Max(0, sections.Count - 1)];
            for (int boundaryIndex = 0; boundaryIndex < result.Length; boundaryIndex++)
            {
                EngineeringResolvedSectionMetadata upstream = sections[boundaryIndex];
                EngineeringResolvedSectionMetadata downstream = sections[boundaryIndex + 1];
                result[boundaryIndex] = new EngineeringSectionBoundaryMetadata(
                    boundaryIndex,
                    downstream.StartStation,
                    upstream.SectionIndex,
                    upstream.SectionId,
                    downstream.SectionIndex,
                    downstream.SectionId);
            }

            return result;
        }

        private static EngineeringControlPointMetadata[] BuildControlPointMetadata(
            IReadOnlyList<GeometricSectionDefinition> sections)
        {
            var result = new List<EngineeringControlPointMetadata>();
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                if (!(sections[sectionIndex] is SpatialSectionDefinition spatial))
                {
                    continue;
                }

                for (int controlPointIndex = 0;
                     controlPointIndex < spatial.ControlPoints.Count;
                     controlPointIndex++)
                {
                    result.Add(new EngineeringControlPointMetadata(
                        spatial.Id,
                        sectionIndex,
                        controlPointIndex,
                        spatial.ControlPoints[controlPointIndex],
                        spatial.Weights[controlPointIndex],
                        authoringId: null));
                }
            }

            return result.ToArray();
        }

        private static EngineeringProfileKeyMetadata[] BuildProfileKeyMetadata(
            IReadOnlyList<BankingProfileKey> keys,
            bool isAuthored)
        {
            var result = new EngineeringProfileKeyMetadata[keys.Count];
            EngineeringProfileKeySource source = isAuthored
                ? EngineeringProfileKeySource.AuthoredBanking
                : EngineeringProfileKeySource.CompiledSectionRoll;

            for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                BankingProfileKey key = keys[keyIndex];
                result[keyIndex] = new EngineeringProfileKeyMetadata(
                    keyIndex,
                    key.Distance,
                    key.RollRadians,
                    key.InterpolationToNext,
                    source,
                    authoringId: null);
            }

            return result;
        }
    }
}
