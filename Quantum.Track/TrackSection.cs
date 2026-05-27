namespace Quantum.Track
{
    /// <summary>
    /// Base type for coaster-domain sections attached to a <see cref="TrackDocument"/>.
    /// </summary>
    /// <remarks>
    /// Sections carry authoring, force, or metadata concepts alongside the centerline.
    /// The base type is intentionally small while section models continue to evolve;
    /// avoid adding engine-specific state here.
    /// </remarks>
    public abstract class TrackSection
    {
    }
}
