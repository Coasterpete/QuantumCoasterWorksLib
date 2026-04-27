using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Parametric curve evaluated over t in [0, 1].
    /// This is the lowest-level curve abstraction.
    /// </summary>
    public interface IParamCurve
    {
        /// <summary>
        /// Evaluate the position on the curve at parameter t.
        /// t is expected to be in the range [0, 1].
        /// </summary>
        Vector3d Evaluate(double t);

        /// <summary>
        /// Evaluate the tangent (first derivative) of the curve at parameter t.
        /// The returned vector is expected to be a normalized direction.
        /// </summary>
        Vector3d Tangent(double t);
    }
}
