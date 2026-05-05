namespace Quantum.Track
{
    public sealed class BuiltInForceEasingFunction : IForceEasingFunction
    {
        private readonly ForceInterpolationMode _mode;
        private readonly ForceInterpolationEvaluator _evaluator;

        public BuiltInForceEasingFunction(ForceInterpolationMode mode)
        {
            _mode = mode;
            _evaluator = new ForceInterpolationEvaluator();
        }

        public double Evaluate(double t)
        {
            return _evaluator.Evaluate(t, _mode);
        }
    }
}
