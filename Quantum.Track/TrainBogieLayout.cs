using System;

namespace Quantum.Track
{
    public sealed class TrainBogieLayout
    {
        public TrainBogieLayout(double bogieSpacing)
        {
            ValidatePositiveFinite(
                bogieSpacing,
                nameof(bogieSpacing),
                "Bogie spacing must be finite and greater than zero.");

            BogieSpacing = bogieSpacing;
        }

        public double BogieSpacing { get; }

        private static void ValidatePositiveFinite(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }
    }
}
