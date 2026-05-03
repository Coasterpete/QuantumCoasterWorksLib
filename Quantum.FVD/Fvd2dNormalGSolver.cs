using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.FVD
{
    /// <summary>
    /// First tiny 2D NormalG solver prototype.
    /// Adjusts exactly one interior control-node Y with one Newton-style step.
    /// </summary>
    public sealed class Fvd2dNormalGSolver
    {
        private const double GravityMps2 = 9.81;

        public Fvd2dNormalGSolverResult Step(FvdGraph graph, Fvd2dNormalGSolverOptions options)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ValidateFinite(options.EvaluationX, nameof(options.EvaluationX));
            ValidateFinite(options.SpeedMps, nameof(options.SpeedMps));

            if (options.SpeedMps < 0.0)
                throw new ArgumentOutOfRangeException(nameof(options.SpeedMps), "Speed must be non-negative.");

            double finiteDifferenceDeltaY = ValidatePositiveFiniteOrFallback(
                options.FiniteDifferenceDeltaY,
                nameof(options.FiniteDifferenceDeltaY),
                fallback: 0.5);

            double maxDeltaYStep = ValidatePositiveFiniteOrFallback(
                options.MaxDeltaYStep,
                nameof(options.MaxDeltaYStep),
                fallback: 1.0);

            double derivativeEpsilon = ValidateStrictlyPositiveFinite(
                options.DerivativeEpsilon,
                nameof(options.DerivativeEpsilon));

            int arcLengthSamples = options.ArcLengthSamples < 2 ? 2 : options.ArcLengthSamples;

            if (!graph.TryEvaluateSectionChannelAt(
                    FvdSectionKind.Force,
                    options.Domain,
                    FvdSectionChannel.NormalG,
                    options.EvaluationX,
                    out double targetNormalG))
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoNormalTarget,
                    beforeAbsoluteNormalGError: 0.0,
                    afterAbsoluteNormalGError: 0.0);
            }

            if (graph.ControlNodes.Count < 3)
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoInteriorNode,
                    beforeAbsoluteNormalGError: 0.0,
                    afterAbsoluteNormalGError: 0.0);
            }

            double beforeRealizedNormalG = EvaluateRealizedNormalGProxy(
                graph,
                options.Domain,
                options.EvaluationX,
                options.SpeedMps,
                arcLengthSamples);

            if (!IsFinite(beforeRealizedNormalG))
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeAbsoluteNormalGError: 0.0,
                    afterAbsoluteNormalGError: 0.0);
            }

            double beforeError = Abs(beforeRealizedNormalG - targetNormalG);

            int interiorIndex = SelectInteriorNodeIndex(graph.ControlNodes.Count);

            FvdGraph plusGraph = CreateGraphWithAdjustedNodeY(graph, interiorIndex, finiteDifferenceDeltaY);
            double plusRealizedNormalG = EvaluateRealizedNormalGProxy(
                plusGraph,
                options.Domain,
                options.EvaluationX,
                options.SpeedMps,
                arcLengthSamples);

            if (!IsFinite(plusRealizedNormalG))
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeError,
                    beforeError);
            }

            double derivative = (plusRealizedNormalG - beforeRealizedNormalG) / finiteDifferenceDeltaY;

            if (!IsFinite(derivative))
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeError,
                    beforeError);
            }

            if (Abs(derivative) <= derivativeEpsilon)
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.FlatDerivative,
                    beforeError,
                    beforeError);
            }

            double signedError = beforeRealizedNormalG - targetNormalG;
            double rawStep = -signedError / derivative;
            double clampedStep = MathUtil.Clamp(rawStep, -maxDeltaYStep, maxDeltaYStep);

            if (Abs(clampedStep) <= derivativeEpsilon)
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeError,
                    beforeError);
            }

            FvdGraph steppedGraph = CreateGraphWithAdjustedNodeY(graph, interiorIndex, clampedStep);
            double afterRealizedNormalG = EvaluateRealizedNormalGProxy(
                steppedGraph,
                options.Domain,
                options.EvaluationX,
                options.SpeedMps,
                arcLengthSamples);

            if (!IsFinite(afterRealizedNormalG))
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeError,
                    beforeError);
            }

            double afterError = Abs(afterRealizedNormalG - targetNormalG);

            if (afterError >= beforeError)
            {
                return new Fvd2dNormalGSolverResult(
                    graph,
                    Fvd2dNormalGSolverStatus.NoImprovement,
                    beforeError,
                    beforeError);
            }

            return new Fvd2dNormalGSolverResult(
                steppedGraph,
                Fvd2dNormalGSolverStatus.Success,
                beforeError,
                afterError);
        }

        private static int SelectInteriorNodeIndex(int controlNodeCount)
        {
            int firstInterior = 1;
            int lastInterior = controlNodeCount - 2;

            return (firstInterior + lastInterior) / 2;
        }

        private static double EvaluateRealizedNormalGProxy(
            FvdGraph graph,
            FvdFunctionDomain domain,
            double evaluationX,
            double speedMps,
            int arcLengthSamples)
        {
            FvdNurbsBuildResult buildResult = graph.BuildNurbsCurve(arcLengthSamples);
            IArcLengthCurve arcCurve = buildResult.ArcCurve;

            double sampledS = MapEvaluationXToCurveDistance(graph, domain, evaluationX, arcCurve.Length);
            double curvature = EstimateCurvatureMagnitude(arcCurve, sampledS);
            double speedSquared = speedMps * speedMps;

            return (speedSquared * curvature) / GravityMps2;
        }

        private static double MapEvaluationXToCurveDistance(
            FvdGraph graph,
            FvdFunctionDomain domain,
            double evaluationX,
            double curveLength)
        {
            if (curveLength <= MathUtil.Epsilon)
                return 0.0;

            if (domain == FvdFunctionDomain.Distance
                && TryResolveForceSectionRange(graph, domain, evaluationX, out double startX, out double endX))
            {
                double span = endX - startX;
                if (span > MathUtil.Epsilon)
                {
                    double normalized = (evaluationX - startX) / span;
                    return MathUtil.Clamp(normalized, 0.0, 1.0) * curveLength;
                }
            }

            return MathUtil.Clamp(evaluationX, 0.0, curveLength);
        }

        private static bool TryResolveForceSectionRange(
            FvdGraph graph,
            FvdFunctionDomain domain,
            double x,
            out double startX,
            out double endX)
        {
            FvdSectionDefinition? finalSection = null;

            IReadOnlyList<FvdSectionDefinition> sections = graph.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                FvdSectionDefinition section = sections[i];

                if (section.Kind != FvdSectionKind.Force || section.Domain != domain)
                    continue;

                if (finalSection == null || section.EndX > finalSection.EndX)
                    finalSection = section;

                if (x >= section.StartX && x < section.EndX)
                {
                    startX = section.StartX;
                    endX = section.EndX;
                    return true;
                }
            }

            if (finalSection != null && x == finalSection.EndX && x >= finalSection.StartX)
            {
                startX = finalSection.StartX;
                endX = finalSection.EndX;
                return true;
            }

            startX = 0.0;
            endX = 0.0;
            return false;
        }

        private static double EstimateCurvatureMagnitude(IArcLengthCurve curve, double s)
        {
            double length = curve.Length;
            if (length <= MathUtil.Epsilon)
                return 0.0;

            double clampedS = MathUtil.Clamp(s, 0.0, length);
            double h = System.Math.Max(length * 1e-3, 1e-4);
            double leftS = MathUtil.Clamp(clampedS - h, 0.0, length);
            double rightS = MathUtil.Clamp(clampedS + h, 0.0, length);
            double span = rightS - leftS;

            if (span <= MathUtil.Epsilon)
                return 0.0;

            Vector3d leftTangent = curve.TangentByLength(leftS);
            Vector3d rightTangent = curve.TangentByLength(rightS);
            Vector3d dTds = (rightTangent - leftTangent) / span;

            if (!IsFinite(dTds))
                return 0.0;

            return dTds.Length;
        }

        private static FvdGraph CreateGraphWithAdjustedNodeY(FvdGraph source, int nodeIndex, double deltaY)
        {
            var nodes = new List<FvdControlNode>(source.ControlNodes.Count);

            for (int i = 0; i < source.ControlNodes.Count; i++)
            {
                FvdControlNode node = source.ControlNodes[i];

                if (i != nodeIndex)
                {
                    nodes.Add(node);
                    continue;
                }

                Vector3d position = node.Position;
                Vector3d adjustedPosition = new Vector3d(position.X, position.Y + deltaY, position.Z);
                nodes.Add(new FvdControlNode(node.U, adjustedPosition, node.Weight));
            }

            return new FvdGraph(
                nodes,
                source.Degree,
                new List<FvdForceSample>(source.ForceSamples),
                new List<FvdSectionDefinition>(source.Sections));
        }

        private static bool IsFinite(Vector3d value)
        {
            return !(double.IsNaN(value.X)
                     || double.IsInfinity(value.X)
                     || double.IsNaN(value.Y)
                     || double.IsInfinity(value.Y)
                     || double.IsNaN(value.Z)
                     || double.IsInfinity(value.Z));
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }

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

        private static double ValidatePositiveFiniteOrFallback(double value, string paramName, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be finite.");
            }

            if (value <= MathUtil.Epsilon)
                return fallback;

            return value;
        }

        private static double ValidateStrictlyPositiveFinite(double value, string paramName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be finite.");
            }

            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be positive.");
            }

            return value;
        }

        private static double Abs(double value)
        {
            return System.Math.Abs(value);
        }
    }
}
