using System;

namespace Quantum.Track
{
    public sealed class TrainCarGeometry
    {
        public TrainCarGeometry(double length, double width, double height)
        {
            TrainValidation.ValidatePositiveDouble(length, nameof(length), "Length must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(width, nameof(width), "Width must be finite and greater than zero.");
            TrainValidation.ValidatePositiveDouble(height, nameof(height), "Height must be finite and greater than zero.");

            Length = length;
            Width = width;
            Height = height;
        }

        public double Length { get; }

        public double Width { get; }

        public double Height { get; }
    }
}
