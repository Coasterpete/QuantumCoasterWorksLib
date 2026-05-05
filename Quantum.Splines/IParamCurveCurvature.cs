namespace Quantum.Splines
{
    /// <summary>
    /// Optional curvature contract for parametric curves evaluated over t in [0, 1].
    /// </summary>
    public interface IParamCurveCurvature
    {
        bool TryGetCurvature(double t, out double curvature);
    }
}
