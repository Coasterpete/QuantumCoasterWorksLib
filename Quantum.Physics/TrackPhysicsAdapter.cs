using System;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using TrackFrame = Quantum.Splines.TrackFrame;

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
            return _evaluator.EvaluateSplineFrameAtDistance(doc, distance);
        }

        public Transform3d GetTransformAtDistance(TrackDocument doc, double distance)
        {
            return _evaluator.EvaluateTransformAtDistance(doc, distance);
        }

        public bool TryGetCurvatureAtDistance(TrackDocument doc, double distance, out double curvature)
        {
            curvature = 0.0;

            if (doc is null)
            {
                return false;
            }

            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                return false;
            }

            double totalLength;
            try
            {
                totalLength = _evaluator.GetTrackTotalLength(doc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (totalLength <= MathUtil.Epsilon)
            {
                return true;
            }

            if (TryGetSplineCurvatureAtDistance(doc, distance, out double splineCurvature))
            {
                curvature = splineCurvature;
                return true;
            }

            double clampedDistance = MathUtil.Clamp(distance, 0.0, totalLength);
            double deltaS = System.Math.Max(totalLength * 1e-3, 1e-4);
            double prevS = MathUtil.Clamp(clampedDistance - deltaS, 0.0, totalLength);
            double nextS = MathUtil.Clamp(clampedDistance + deltaS, 0.0, totalLength);
            double spanS = nextS - prevS;

            if (spanS <= MathUtil.Epsilon)
            {
                return true;
            }

            TrackFrame prevFrame;
            TrackFrame nextFrame;
            try
            {
                prevFrame = _evaluator.EvaluateSplineFrameAtDistance(doc, prevS);
                nextFrame = _evaluator.EvaluateSplineFrameAtDistance(doc, nextS);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            Vector3d deltaTangent = nextFrame.Tangent - prevFrame.Tangent;
            curvature = deltaTangent.Length / spanS;

            if (double.IsNaN(curvature) || double.IsInfinity(curvature))
            {
                curvature = 0.0;
                return false;
            }

            return true;
        }

        private bool TryGetSplineCurvatureAtDistance(TrackDocument doc, double distance, out double curvature)
        {
            curvature = 0.0;

            TrackEvaluationPoint evaluationPoint;
            try
            {
                evaluationPoint = _evaluator.EvaluateAtDistance(doc, distance);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (!(evaluationPoint.Segment.Spline is IParamCurveCurvature curvatureCurve))
            {
                return false;
            }

            if (!curvatureCurve.TryGetCurvature(evaluationPoint.LocalT, out double splineCurvature))
            {
                return false;
            }

            if (double.IsNaN(splineCurvature) || double.IsInfinity(splineCurvature))
            {
                return false;
            }

            curvature = System.Math.Abs(splineCurvature);
            return true;
        }
    }
}
