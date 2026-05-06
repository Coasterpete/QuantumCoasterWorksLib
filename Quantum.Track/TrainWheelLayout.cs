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
            TrainValidation.ValidatePositiveInt(
                wheelCountPerBogie,
                nameof(wheelCountPerBogie),
                "Wheel count per bogie must be greater than zero.");
            TrainValidation.ValidatePositiveDouble(
                wheelRadius,
                nameof(wheelRadius),
                "Wheel radius must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(
                wheelWidth,
                nameof(wheelWidth),
                "Wheel width must be finite and greater than zero.");
            TrainValidation.ValidateNonNegativeDouble(
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
    }
}
