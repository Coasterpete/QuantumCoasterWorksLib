namespace Quantum.Track
{
    /// <summary>
    /// Evaluates a force channel at normalized section position <c>t</c>.
    /// </summary>
    /// <remarks>
    /// Callers pass <c>t</c> in the normalized section interval <c>[0, 1]</c>. The
    /// meaning of the returned value is determined by the channel path: plural force
    /// channel lists use it as a direct target value, while single normal/lateral/
    /// longitudinal channels use it as a scalar interpolation parameter.
    /// </remarks>
    public interface IForceChannel
    {
        /// <summary>
        /// Evaluates the channel at normalized section position <paramref name="t"/>.
        /// </summary>
        double Evaluate(double t);
    }
}
