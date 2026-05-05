using System;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Physics
{
    /// <summary>
    /// Read-only bridge for sampling track geometry from physics systems.
    /// </summary>
    public sealed class TrackPhysicsAdapter
    {
        private readonly TrackEvaluator _evaluator;

        public TrackPhysicsAdapter()
            : this(new TrackEvaluator())
        {
        }

        public TrackPhysicsAdapter(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public TrackFrame GetFrameAtDistance(TrackDocument doc, double distance)
        {
            return _evaluator.EvaluateFrameAtDistance(doc, distance);
        }

        public Transform3d GetTransformAtDistance(TrackDocument doc, double distance)
        {
            return _evaluator.EvaluateTransformAtDistance(doc, distance);
        }
    }
}
