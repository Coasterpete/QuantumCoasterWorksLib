using Quantum.Math;

namespace Quantum.FVD
{
    /// <summary>
    /// Minimal options for the first 2D NormalG single-step solver prototype.
    /// </summary>
    public sealed class Fvd2dNormalGSolverOptions
    {
        public FvdFunctionDomain Domain { get; set; } = FvdFunctionDomain.Distance;

        public double EvaluationX { get; set; }

        public double SpeedMps { get; set; } = 20.0;

        public double FiniteDifferenceDeltaY { get; set; } = 0.5;

        public double MaxDeltaYStep { get; set; } = 1.0;

        public double DerivativeEpsilon { get; set; } = MathUtil.Epsilon;

        public int ArcLengthSamples { get; set; } = 256;

        /// <summary>
        /// When enabled, Step(...) tries interior nodes in deterministic order until one succeeds.
        /// Default false preserves legacy single-midpoint behavior.
        /// </summary>
        public bool EnableDeterministicInteriorNodeSweep { get; set; }

        /// <summary>
        /// Starting interior-node offset used when deterministic sweep is enabled.
        /// Zero starts from the first interior node; values are wrapped modulo interior count.
        /// </summary>
        public int InteriorNodeSweepStartIndex { get; set; }
    }
}
