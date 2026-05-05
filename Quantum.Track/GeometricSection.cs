namespace Quantum.Track
{
    public sealed class GeometricSection : TrackSection
    {
        public GeometricSection(
            double length,
            double? curvature = null,
            double? roll = null)
        {
            Length = length;
            Curvature = curvature;
            Roll = roll;
        }

        public double Length { get; }

        public double? Curvature { get; }

        public double? Roll { get; }
    }
}
