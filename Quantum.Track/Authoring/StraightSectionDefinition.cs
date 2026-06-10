namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Authoring definition for a straight centerline section.
    /// </summary>
    public sealed class StraightSectionDefinition : GeometricSectionDefinition
    {
        public StraightSectionDefinition(
            string id,
            double length,
            double rollRadians = 0.0)
            : base(id, length, rollRadians)
        {
        }
    }
}
