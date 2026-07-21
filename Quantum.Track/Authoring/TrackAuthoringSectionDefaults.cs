using System;

namespace Quantum.Track.Authoring
{
    [Flags]
    public enum TrackAuthoringSectionDefaultFlags
    {
        None = 0,
        RollInheritedFromUpstream = 1 << 0,
        RollInheritedFromDownstream = 1 << 1,
        ZeroRollFallback = 1 << 2,
        CurvatureInheritedFromUpstream = 1 << 3,
        CurvatureInheritedFromDownstream = 1 << 4,
        TransitionBridgesNeighbors = 1 << 5,
        PositiveRadiusFallback = 1 << 6,
        ZeroCurvatureFallback = 1 << 7,
        UpstreamScalarCurvatureUnavailable = 1 << 8,
        DownstreamScalarCurvatureUnavailable = 1 << 9,
        InsertionStationUnavailable = 1 << 10,
        UpstreamRollUnavailable = 1 << 11,
        DownstreamRollUnavailable = 1 << 12
    }

    /// <summary>
    /// Deterministic production section defaults and the route context that produced them.
    /// </summary>
    public sealed class TrackAuthoringSectionDefaults
    {
        internal TrackAuthoringSectionDefaults(
            TrackAuthoringSectionDefinition definition,
            double? insertionStation,
            string? upstreamNodeId,
            string? downstreamNodeId,
            double? upstreamEndCurvature,
            double? downstreamStartCurvature,
            double inheritedRollRadians,
            TrackAuthoringSectionDefaultFlags flags)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            InsertionStation = insertionStation;
            UpstreamNodeId = upstreamNodeId;
            DownstreamNodeId = downstreamNodeId;
            UpstreamEndCurvature = upstreamEndCurvature;
            DownstreamStartCurvature = downstreamStartCurvature;
            InheritedRollRadians = inheritedRollRadians;
            Flags = flags;
        }

        public TrackAuthoringSectionDefinition Definition { get; }

        /// <summary>
        /// Start station of the insertion or replacement position in station-distance units.
        /// </summary>
        public double? InsertionStation { get; }

        public bool HasInsertionStation => InsertionStation.HasValue;

        public string? UpstreamNodeId { get; }

        public string? DownstreamNodeId { get; }

        public double? UpstreamEndCurvature { get; }

        public double? DownstreamStartCurvature { get; }

        public double InheritedRollRadians { get; }

        public TrackAuthoringSectionDefaultFlags Flags { get; }
    }
}
