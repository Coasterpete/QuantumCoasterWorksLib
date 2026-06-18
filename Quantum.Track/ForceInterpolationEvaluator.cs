using Quantum.Track.Internal;

namespace Quantum.Track
{
    public sealed class ForceInterpolationEvaluator
    {
        public double Evaluate(double t, ForceInterpolationMode mode)
        {
            return ScalarEasing.Evaluate(t, ScalarEasing.MapForceInterpolationMode(mode));
        }
    }
}
