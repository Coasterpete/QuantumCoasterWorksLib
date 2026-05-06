using System;

namespace Quantum.Track
{
    public sealed class TrainConsistDefinition
    {
        public TrainConsistDefinition(
            int carCount,
            double carSpacing,
            double carLength,
            double carWidth,
            double carHeight,
            double bogieSpacing)
            : this(
                carCount,
                carSpacing,
                carLength,
                carWidth,
                carHeight,
                bogieSpacing,
                wheelLayout: null)
        {
        }

        public TrainConsistDefinition(
            int carCount,
            double carSpacing,
            double carLength,
            double carWidth,
            double carHeight,
            double bogieSpacing,
            TrainWheelLayout? wheelLayout)
        {
            TrainValidation.ValidatePositiveInt(carCount, nameof(carCount), "Car count must be greater than zero.");
            TrainValidation.ValidatePositiveDouble(carSpacing, nameof(carSpacing), "Car spacing must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(carLength, nameof(carLength), "Car length must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(carWidth, nameof(carWidth), "Car width must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(carHeight, nameof(carHeight), "Car height must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(bogieSpacing, nameof(bogieSpacing), "Bogie spacing must be finite and greater than zero.");

            if (bogieSpacing > carLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bogieSpacing),
                    bogieSpacing,
                    "Bogie spacing must be less than or equal to car length.");
            }

            CarCount = carCount;
            CarSpacing = carSpacing;
            CarGeometry = new TrainCarGeometry(carLength, carWidth, carHeight);
            BogieLayout = new TrainBogieLayout(bogieSpacing);
            WheelLayout = wheelLayout;
        }

        public TrainConsistDefinition(
            int carCount,
            double carSpacing,
            TrainCarGeometry carGeometry,
            TrainBogieLayout bogieLayout)
            : this(
                carCount,
                carSpacing,
                carGeometry,
                bogieLayout,
                wheelLayout: null)
        {
        }

        public TrainConsistDefinition(
            int carCount,
            double carSpacing,
            TrainCarGeometry carGeometry,
            TrainBogieLayout bogieLayout,
            TrainWheelLayout? wheelLayout)
        {
            if (carGeometry == null)
            {
                throw new ArgumentNullException(nameof(carGeometry));
            }

            if (bogieLayout == null)
            {
                throw new ArgumentNullException(nameof(bogieLayout));
            }

            TrainValidation.ValidatePositiveInt(carCount, nameof(carCount), "Car count must be greater than zero.");
            TrainValidation.ValidatePositiveDouble(carSpacing, nameof(carSpacing), "Car spacing must be finite and greater than zero.");

            if (bogieLayout.BogieSpacing > carGeometry.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bogieLayout),
                    bogieLayout.BogieSpacing,
                    "Bogie spacing must be less than or equal to car length.");
            }

            CarCount = carCount;
            CarSpacing = carSpacing;
            CarGeometry = carGeometry;
            BogieLayout = bogieLayout;
            WheelLayout = wheelLayout;
        }

        public int CarCount { get; }

        public double CarSpacing { get; }

        public TrainCarGeometry CarGeometry { get; }

        public TrainBogieLayout BogieLayout { get; }

        public TrainWheelLayout? WheelLayout { get; }

        public double CarLength => CarGeometry.Length;

        public double CarWidth => CarGeometry.Width;

        public double CarHeight => CarGeometry.Height;

        public double BogieSpacing => BogieLayout.BogieSpacing;
    }
}
