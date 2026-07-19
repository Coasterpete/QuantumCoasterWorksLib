using System;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Immutable request for one revisioned, uniform station-distance engineering snapshot.
    /// </summary>
    /// <remarks>
    /// Revision values are assigned by the authoring host. The compilation revision
    /// identifies the active compiled authoring state, while the snapshot revision
    /// identifies this requested projection of that state.
    /// </remarks>
    public sealed class EngineeringSnapshotRequest
    {
        public EngineeringSnapshotRequest(
            long compilationRevision,
            long snapshotRevision,
            int stationSampleCount)
        {
            if (compilationRevision < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compilationRevision),
                    compilationRevision,
                    "Compilation revision must be non-negative.");
            }

            if (snapshotRevision < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(snapshotRevision),
                    snapshotRevision,
                    "Snapshot revision must be non-negative.");
            }

            if (stationSampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stationSampleCount),
                    stationSampleCount,
                    "Station sample count must be at least two.");
            }

            CompilationRevision = compilationRevision;
            SnapshotRevision = snapshotRevision;
            StationSampleCount = stationSampleCount;
        }

        /// <summary>
        /// Host-owned revision of the graph/compilation state used as the source.
        /// </summary>
        public long CompilationRevision { get; }

        /// <summary>
        /// Host-owned revision of the engineering snapshot projection.
        /// </summary>
        public long SnapshotRevision { get; }

        /// <summary>
        /// Number of uniformly spaced canonical station samples, including both endpoints.
        /// </summary>
        public int StationSampleCount { get; }
    }

    /// <summary>
    /// Immutable revision identity carried by an engineering snapshot.
    /// </summary>
    public sealed class EngineeringSnapshotRevisionMetadata
    {
        public const int CurrentContractVersion = 1;

        internal EngineeringSnapshotRevisionMetadata(
            long compilationRevision,
            long snapshotRevision)
        {
            ContractVersion = CurrentContractVersion;
            CompilationRevision = compilationRevision;
            SnapshotRevision = snapshotRevision;
        }

        /// <summary>
        /// Version of the in-memory engineering snapshot contract.
        /// </summary>
        public int ContractVersion { get; }

        public long CompilationRevision { get; }

        public long SnapshotRevision { get; }
    }

    /// <summary>
    /// Immutable sampling policy and compiled-runtime tolerances used by a snapshot.
    /// </summary>
    public sealed class EngineeringSnapshotSamplingMetadata
    {
        internal EngineeringSnapshotSamplingMetadata(
            int stationSampleCount,
            int arcLengthSampleCount,
            double arcLengthTolerance,
            int transportSamplesPerSegment)
        {
            StationSampleCount = stationSampleCount;
            ArcLengthSampleCount = arcLengthSampleCount;
            ArcLengthTolerance = arcLengthTolerance;
            TransportSamplesPerSegment = transportSamplesPerSegment;
        }

        /// <summary>
        /// Uniform station-grid sample count, including exact start and end stations.
        /// </summary>
        public int StationSampleCount { get; }

        public int ArcLengthSampleCount { get; }

        public double ArcLengthTolerance { get; }

        public int TransportSamplesPerSegment { get; }
    }
}
