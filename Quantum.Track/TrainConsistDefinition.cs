using System;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable dimensions and layout settings for evaluating a coaster train pose.
    /// </summary>
    /// <remarks>
    /// The consist definition describes spacing and simple box/running-gear geometry.
    /// It is engine-agnostic and is consumed by <see cref="TrainCarTransformProvider"/>
    /// when placing cars along a bound centerline.
    /// </remarks>
    public sealed class TrainConsistDefinition
    {
        /// <summary>
        /// Creates a consist definition from scalar car and bogie dimensions.
        /// </summary>
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

        /// <summary>
        /// Creates a consist definition from scalar car, bogie, and optional wheel dimensions.
        /// </summary>
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

        /// <summary>
        /// Creates a consist definition from reusable geometry and bogie layout values.
        /// </summary>
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

        /// <summary>
        /// Creates a consist definition from reusable geometry, bogie layout, and optional wheel layout values.
        /// </summary>
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

        /// <summary>
        /// Number of cars in the train.
        /// </summary>
        public int CarCount { get; }

        /// <summary>
        /// Station-distance spacing between adjacent car centers.
        /// </summary>
        public double CarSpacing { get; }

        /// <summary>
        /// Box dimensions used for each car body.
        /// </summary>
        public TrainCarGeometry CarGeometry { get; }

        /// <summary>
        /// Bogie layout used for each car body.
        /// </summary>
        public TrainBogieLayout BogieLayout { get; }

        /// <summary>
        /// Optional wheel layout used when wheel transforms are requested.
        /// </summary>
        public TrainWheelLayout? WheelLayout { get; }

        /// <summary>
        /// Car body length.
        /// </summary>
        public double CarLength => CarGeometry.Length;

        /// <summary>
        /// Car body width.
        /// </summary>
        public double CarWidth => CarGeometry.Width;

        /// <summary>
        /// Car body height.
        /// </summary>
        public double CarHeight => CarGeometry.Height;

        /// <summary>
        /// Distance between the front and rear bogie centers.
        /// </summary>
        public double BogieSpacing => BogieLayout.BogieSpacing;
    }
}
