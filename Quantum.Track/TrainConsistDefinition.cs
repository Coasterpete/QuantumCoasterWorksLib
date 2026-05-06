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
            ValidateCarCount(carCount);
            ValidatePositiveFinite(carSpacing, nameof(carSpacing), "Car spacing must be finite and greater than zero.");
            ValidatePositiveFinite(carLength, nameof(carLength), "Car length must be finite and greater than zero.");
            ValidatePositiveFinite(carWidth, nameof(carWidth), "Car width must be finite and greater than zero.");
            ValidatePositiveFinite(carHeight, nameof(carHeight), "Car height must be finite and greater than zero.");
            ValidatePositiveFinite(bogieSpacing, nameof(bogieSpacing), "Bogie spacing must be finite and greater than zero.");

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

            ValidateCarCount(carCount);
            ValidatePositiveFinite(carSpacing, nameof(carSpacing), "Car spacing must be finite and greater than zero.");

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

        private static void ValidateCarCount(int carCount)
        {
            if (carCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carCount),
                    carCount,
                    "Car count must be greater than zero.");
            }
        }

        private static void ValidatePositiveFinite(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }
    }
}
