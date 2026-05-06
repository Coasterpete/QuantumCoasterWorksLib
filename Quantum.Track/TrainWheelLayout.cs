using System;

namespace Quantum.Track
{
    public sealed class TrainWheelLayout
    {
        public TrainWheelLayout(
            int wheelCountPerBogie,
            double wheelRadius,
            double wheelWidth,
            double axleSpacing)
        {
            ValidateWheelCountPerBogie(wheelCountPerBogie);
            ValidatePositiveFinite(
                wheelRadius,
                nameof(wheelRadius),
                "Wheel radius must be finite and greater than zero.");
            ValidatePositiveFinite(
                wheelWidth,
                nameof(wheelWidth),
                "Wheel width must be finite and greater than zero.");
            ValidateNonNegativeFinite(
                axleSpacing,
                nameof(axleSpacing),
                "Axle spacing must be finite and greater than or equal to zero.");

            WheelCountPerBogie = wheelCountPerBogie;
            WheelRadius = wheelRadius;
            WheelWidth = wheelWidth;
            AxleSpacing = axleSpacing;
        }

        public int WheelCountPerBogie { get; }

        public double WheelRadius { get; }

        public double WheelWidth { get; }

        public double AxleSpacing { get; }

        private static void ValidateWheelCountPerBogie(int wheelCountPerBogie)
        {
            if (wheelCountPerBogie <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wheelCountPerBogie),
                    wheelCountPerBogie,
                    "Wheel count per bogie must be greater than zero.");
            }
        }

        private static void ValidatePositiveFinite(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }
    }
}
