using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// A curve that supports arc-length based evaluation.
    /// </summary>
    public interface IArcLengthCurve : IParamCurve
    {
        /// <summary>
        /// Total length of the curve.
        /// </summary>
        double Length { get; }

        /// <summary>
        /// Evaluate position at arc-length s (0..Length).
        /// </summary>
        Vector3d EvaluateByLength(double s);

        /// <summary>
        /// Evaluate tangent at arc-length s (0..Length).
        /// </summary>
        Vector3d TangentByLength(double s);
    }
}
