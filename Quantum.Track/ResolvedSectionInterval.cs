using System;

namespace Quantum.Track
{
    public sealed class ResolvedSectionInterval<TSection>
    {
        public ResolvedSectionInterval(
            TSection section,
            double startDistance,
            double endDistance,
            bool includeEndDistance = false)
        {
            if (double.IsNaN(startDistance) || double.IsInfinity(startDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDistance),
                    startDistance,
                    "StartDistance must be finite.");
            }

            if (double.IsNaN(endDistance) || double.IsInfinity(endDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endDistance),
                    endDistance,
                    "EndDistance must be finite.");
            }

            if (endDistance < startDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endDistance),
                    endDistance,
                    "EndDistance must be greater than or equal to StartDistance.");
            }

            Section = section;
            StartDistance = startDistance;
            EndDistance = endDistance;
            IncludeEndDistance = includeEndDistance;
        }

        public TSection Section { get; }

        public double StartDistance { get; }

        public double EndDistance { get; }

        public double Length => EndDistance - StartDistance;

        public bool IncludeEndDistance { get; }

        public bool Contains(double distance)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                return false;
            }

            if (distance < StartDistance)
            {
                return false;
            }

            if (distance < EndDistance)
            {
                return true;
            }

            return IncludeEndDistance && distance == EndDistance;
        }
    }
}
