using System;
using System.Threading;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Scoped, opt-in measurement for the synchronous authoring pipeline.
    /// </summary>
    /// <remarks>
    /// This internal hook keeps production APIs unchanged while allowing editor
    /// integration tests and diagnostic harnesses to observe otherwise hidden
    /// compiler and projection work. Measurements flow with the current async
    /// context and do not interfere with measurements in parallel tests.
    /// </remarks>
    internal sealed class TrackAuthoringPipelineMeasurement : IDisposable
    {
        private static readonly AsyncLocal<TrackAuthoringPipelineMeasurement?> CurrentValue =
            new AsyncLocal<TrackAuthoringPipelineMeasurement?>();

        private readonly TrackAuthoringPipelineMeasurement? _previous;
        private bool _disposed;

        private TrackAuthoringPipelineMeasurement()
        {
            _previous = CurrentValue.Value;
            CurrentValue.Value = this;
        }

        public int GraphCompilerInvocationCount { get; private set; }

        public TimeSpan GraphCompilerElapsed { get; private set; }

        public int EngineeringSnapshotBuildCount { get; private set; }

        public TimeSpan EngineeringSnapshotBuildElapsed { get; private set; }

        public static TrackAuthoringPipelineMeasurement Begin()
        {
            return new TrackAuthoringPipelineMeasurement();
        }

        internal static void RecordGraphCompilation(TimeSpan elapsed)
        {
            TrackAuthoringPipelineMeasurement? current = CurrentValue.Value;
            if (current is null)
            {
                return;
            }

            current.GraphCompilerInvocationCount++;
            current.GraphCompilerElapsed += elapsed;
        }

        internal static void RecordEngineeringSnapshotBuild(TimeSpan elapsed)
        {
            TrackAuthoringPipelineMeasurement? current = CurrentValue.Value;
            if (current is null)
            {
                return;
            }

            current.EngineeringSnapshotBuildCount++;
            current.EngineeringSnapshotBuildElapsed += elapsed;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (ReferenceEquals(CurrentValue.Value, this))
            {
                CurrentValue.Value = _previous;
            }

            _disposed = true;
        }
    }
}
