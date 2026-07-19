using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Identifies whether profile-key metadata came from explicit authoring or
    /// from the compiled section-roll compatibility profile.
    /// </summary>
    public enum EngineeringProfileKeySource
    {
        AuthoredBanking = 0,
        CompiledSectionRoll = 1
    }

    /// <summary>
    /// Immutable evaluated centerline geometry at one canonical station.
    /// </summary>
    public readonly struct EngineeringGeometrySample
    {
        internal EngineeringGeometrySample(
            int sampleIndex,
            double station,
            Vector3d position,
            Vector3d tangent,
            double? curvatureMagnitude)
        {
            SampleIndex = sampleIndex;
            Station = station;
            Position = position;
            Tangent = tangent;
            CurvatureMagnitude = curvatureMagnitude;
        }

        public int SampleIndex { get; }

        public double Station { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        /// <summary>
        /// Existing evaluator-owned unsigned centerline curvature, when available.
        /// </summary>
        public double? CurvatureMagnitude { get; }

        public bool HasCurvature => CurvatureMagnitude.HasValue;
    }

    /// <summary>
    /// Immutable copy of one resolved compiled section interval.
    /// </summary>
    public readonly struct EngineeringResolvedSectionMetadata
    {
        internal EngineeringResolvedSectionMetadata(
            int sectionIndex,
            string sectionId,
            double startStation,
            double endStation,
            bool includesEndStation)
        {
            SectionIndex = sectionIndex;
            SectionId = sectionId;
            StartStation = startStation;
            EndStation = endStation;
            IncludesEndStation = includesEndStation;
        }

        public int SectionIndex { get; }

        /// <summary>
        /// Stable authoring identity supplied by the source section definition.
        /// </summary>
        public string SectionId { get; }

        public double StartStation { get; }

        public double EndStation { get; }

        public bool IncludesEndStation { get; }
    }

    /// <summary>
    /// Immutable resolved boundary between two adjacent compiled sections.
    /// </summary>
    public readonly struct EngineeringSectionBoundaryMetadata
    {
        internal EngineeringSectionBoundaryMetadata(
            int boundaryIndex,
            double station,
            int upstreamSectionIndex,
            string upstreamSectionId,
            int downstreamSectionIndex,
            string downstreamSectionId)
        {
            BoundaryIndex = boundaryIndex;
            Station = station;
            UpstreamSectionIndex = upstreamSectionIndex;
            UpstreamSectionId = upstreamSectionId;
            DownstreamSectionIndex = downstreamSectionIndex;
            DownstreamSectionId = downstreamSectionId;
        }

        public int BoundaryIndex { get; }

        public double Station { get; }

        public int UpstreamSectionIndex { get; }

        public string UpstreamSectionId { get; }

        public int DownstreamSectionIndex { get; }

        public string DownstreamSectionId { get; }
    }

    /// <summary>
    /// Immutable source metadata for one spatial-section control point.
    /// </summary>
    public readonly struct EngineeringControlPointMetadata
    {
        internal EngineeringControlPointMetadata(
            string sectionId,
            int sectionIndex,
            int controlPointIndex,
            Vector3d localPosition,
            double weight,
            string? authoringId)
        {
            SectionId = sectionId;
            SectionIndex = sectionIndex;
            ControlPointIndex = controlPointIndex;
            LocalPosition = localPosition;
            Weight = weight;
            AuthoringId = authoringId;
        }

        /// <summary>
        /// Stable ID of the owning section.
        /// </summary>
        public string SectionId { get; }

        public int SectionIndex { get; }

        /// <summary>
        /// Source-order index. This is not presented as an identity across reordering.
        /// </summary>
        public int ControlPointIndex { get; }

        /// <summary>
        /// Position in the spatial section's local construction frame.
        /// </summary>
        public Vector3d LocalPosition { get; }

        public double Weight { get; }

        /// <summary>
        /// Intrinsic authoring ID when the source model provides one; otherwise null.
        /// </summary>
        public string? AuthoringId { get; }
    }

    /// <summary>
    /// Immutable source metadata for one compiled banking-profile key.
    /// </summary>
    public readonly struct EngineeringProfileKeyMetadata
    {
        internal EngineeringProfileKeyMetadata(
            int keyIndex,
            double station,
            double rollRadians,
            BankingProfileInterpolationMode interpolationToNext,
            EngineeringProfileKeySource source,
            string? authoringId)
        {
            KeyIndex = keyIndex;
            Station = station;
            RollRadians = rollRadians;
            InterpolationToNext = interpolationToNext;
            Source = source;
            AuthoringId = authoringId;
        }

        /// <summary>
        /// Source-order index. This is not presented as an identity across reordering.
        /// </summary>
        public int KeyIndex { get; }

        public double Station { get; }

        public double RollRadians { get; }

        public BankingProfileInterpolationMode InterpolationToNext { get; }

        public EngineeringProfileKeySource Source { get; }

        /// <summary>
        /// Intrinsic authoring ID when the source model provides one; otherwise null.
        /// </summary>
        public string? AuthoringId { get; }
    }

    /// <summary>
    /// Revisioned immutable backend source for station-domain engineering consumers.
    /// </summary>
    /// <remarks>
    /// Every station-indexed collection is aligned with <see cref="StationGrid"/>.
    /// The snapshot retains no mutable <see cref="TrackDocument"/> or source
    /// compilation reference.
    /// </remarks>
    public sealed class EngineeringSnapshot
    {
        private readonly IReadOnlyList<double> _stationGrid;
        private readonly IReadOnlyList<EngineeringGeometrySample> _geometry;
        private readonly IReadOnlyList<TrackFrame> _orientationFrames;
        private readonly IReadOnlyList<double> _bankingRollRadians;
        private readonly IReadOnlyList<EngineeringResolvedSectionMetadata> _resolvedSections;
        private readonly IReadOnlyList<EngineeringSectionBoundaryMetadata> _sectionBoundaries;
        private readonly IReadOnlyList<EngineeringControlPointMetadata> _controlPoints;
        private readonly IReadOnlyList<EngineeringProfileKeyMetadata> _profileKeys;

        internal EngineeringSnapshot(
            EngineeringSnapshotRevisionMetadata revision,
            EngineeringSnapshotSamplingMetadata sampling,
            double totalLength,
            IEnumerable<double> stationGrid,
            IEnumerable<EngineeringGeometrySample> geometry,
            IEnumerable<TrackFrame> orientationFrames,
            IEnumerable<double> bankingRollRadians,
            IEnumerable<EngineeringResolvedSectionMetadata> resolvedSections,
            IEnumerable<EngineeringSectionBoundaryMetadata> sectionBoundaries,
            IEnumerable<EngineeringControlPointMetadata> controlPoints,
            IEnumerable<EngineeringProfileKeyMetadata> profileKeys)
        {
            Revision = revision ?? throw new ArgumentNullException(nameof(revision));
            Sampling = sampling ?? throw new ArgumentNullException(nameof(sampling));
            TotalLength = totalLength;
            _stationGrid = Copy(stationGrid, nameof(stationGrid));
            _geometry = Copy(geometry, nameof(geometry));
            _orientationFrames = Copy(orientationFrames, nameof(orientationFrames));
            _bankingRollRadians = Copy(bankingRollRadians, nameof(bankingRollRadians));
            _resolvedSections = Copy(resolvedSections, nameof(resolvedSections));
            _sectionBoundaries = Copy(sectionBoundaries, nameof(sectionBoundaries));
            _controlPoints = Copy(controlPoints, nameof(controlPoints));
            _profileKeys = Copy(profileKeys, nameof(profileKeys));

            int stationCount = _stationGrid.Count;
            if (_geometry.Count != stationCount ||
                _orientationFrames.Count != stationCount ||
                _bankingRollRadians.Count != stationCount)
            {
                throw new ArgumentException(
                    "All station-indexed engineering snapshot collections must be aligned.");
            }
        }

        public EngineeringSnapshotRevisionMetadata Revision { get; }

        public EngineeringSnapshotSamplingMetadata Sampling { get; }

        public double TotalLength { get; }

        public int SampleCount => _stationGrid.Count;

        public IReadOnlyList<double> StationGrid => _stationGrid;

        public IReadOnlyList<EngineeringGeometrySample> Geometry => _geometry;

        /// <summary>
        /// Canonical transported frames with the compiled banking profile applied.
        /// </summary>
        public IReadOnlyList<TrackFrame> OrientationFrames => _orientationFrames;

        /// <summary>
        /// Evaluated compiled banking profile aligned to <see cref="StationGrid"/>.
        /// </summary>
        public IReadOnlyList<double> BankingRollRadians => _bankingRollRadians;

        public IReadOnlyList<EngineeringResolvedSectionMetadata> ResolvedSections =>
            _resolvedSections;

        public IReadOnlyList<EngineeringSectionBoundaryMetadata> SectionBoundaries =>
            _sectionBoundaries;

        public IReadOnlyList<EngineeringControlPointMetadata> ControlPoints => _controlPoints;

        public IReadOnlyList<EngineeringProfileKeyMetadata> ProfileKeys => _profileKeys;

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source, string paramName)
        {
            if (source is null)
            {
                throw new ArgumentNullException(paramName);
            }

            return new List<T>(source).AsReadOnly();
        }
    }
}
