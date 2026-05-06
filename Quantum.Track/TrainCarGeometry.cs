using System;

namespace Quantum.Track
{
    public sealed class TrainCarGeometry
    {
        public TrainCarGeometry(double length, double width, double height)
        {
            ValidatePositiveFinite(length, nameof(length), "Length must be finite and greater than zero.");
            ValidatePositiveFinite(width, nameof(width), "Width must be finite and greater than zero.");
            ValidatePositiveFinite(height, nameof(height), "Height must be finite and greater than zero.");

            Length = length;
            Width = width;
            Height = height;
        }

        public double Length { get; }

        public double Width { get; }

        public double Height { get; }

        private static void ValidatePositiveFinite(double value, string parameterName, string message)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, message);
            }
        }
    }
}
