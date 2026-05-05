using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track
{
    public static class TrainCarDebugGizmoBuilder
    {
        public static DebugLineSegment[] BuildWireBox(
            TrainCarTransform car,
            double length,
            double width,
            double height)
        {
            ValidateDimension(length, nameof(length));
            ValidateDimension(width, nameof(width));
            ValidateDimension(height, nameof(height));

            return BuildWireBoxCore(car, length, width, height);
        }

        public static DebugLineSegment[] BuildWireBoxes(
            IEnumerable<TrainCarTransform> cars,
            double length,
            double width,
            double height)
        {
            if (cars is null)
            {
                throw new ArgumentNullException(nameof(cars));
            }

            ValidateDimension(length, nameof(length));
            ValidateDimension(width, nameof(width));
            ValidateDimension(height, nameof(height));

            var segments = new List<DebugLineSegment>();
            foreach (TrainCarTransform car in cars)
            {
                segments.AddRange(BuildWireBoxCore(car, length, width, height));
            }

            return segments.ToArray();
        }

        private static DebugLineSegment[] BuildWireBoxCore(
            TrainCarTransform car,
            double length,
            double width,
            double height)
        {
            Vector3d center = car.Frame.Position;
            Vector3d tangentHalf = car.Frame.Tangent * (length * 0.5);
            Vector3d normalHalf = car.Frame.Normal * (height * 0.5);
            Vector3d binormalHalf = car.Frame.Binormal * (width * 0.5);

            Vector3d c000 = center - tangentHalf - normalHalf - binormalHalf;
            Vector3d c001 = center - tangentHalf - normalHalf + binormalHalf;
            Vector3d c010 = center - tangentHalf + normalHalf - binormalHalf;
            Vector3d c011 = center - tangentHalf + normalHalf + binormalHalf;
            Vector3d c100 = center + tangentHalf - normalHalf - binormalHalf;
            Vector3d c101 = center + tangentHalf - normalHalf + binormalHalf;
            Vector3d c110 = center + tangentHalf + normalHalf - binormalHalf;
            Vector3d c111 = center + tangentHalf + normalHalf + binormalHalf;

            return new[]
            {
                new DebugLineSegment(c000, c100, TrackFrameAxisType.Tangent),
                new DebugLineSegment(c001, c101, TrackFrameAxisType.Tangent),
                new DebugLineSegment(c010, c110, TrackFrameAxisType.Tangent),
                new DebugLineSegment(c011, c111, TrackFrameAxisType.Tangent),

                new DebugLineSegment(c000, c010, TrackFrameAxisType.Normal),
                new DebugLineSegment(c001, c011, TrackFrameAxisType.Normal),
                new DebugLineSegment(c100, c110, TrackFrameAxisType.Normal),
                new DebugLineSegment(c101, c111, TrackFrameAxisType.Normal),

                new DebugLineSegment(c000, c001, TrackFrameAxisType.Binormal),
                new DebugLineSegment(c010, c011, TrackFrameAxisType.Binormal),
                new DebugLineSegment(c100, c101, TrackFrameAxisType.Binormal),
                new DebugLineSegment(c110, c111, TrackFrameAxisType.Binormal)
            };
        }

        private static void ValidateDimension(double dimension, string paramName)
        {
            if (double.IsNaN(dimension) || double.IsInfinity(dimension) || dimension <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    dimension,
                    "Dimension must be finite and greater than zero.");
            }
        }
    }
}
