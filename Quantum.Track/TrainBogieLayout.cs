using System;

namespace Quantum.Track
{
    public sealed class TrainBogieLayout
    {
        public TrainBogieLayout(double bogieSpacing)
        {
            TrainValidation.ValidatePositiveDouble(
                bogieSpacing,
                nameof(bogieSpacing),
                "Bogie spacing must be finite and greater than zero.");

            BogieSpacing = bogieSpacing;
        }

        public double BogieSpacing { get; }
    }
}
