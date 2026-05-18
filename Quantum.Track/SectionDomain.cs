namespace Quantum.Track
{
    /// <summary>
    /// Domain used by normalized section definitions.
    /// Time-domain normalized sections are data-only until elapsed-time
    /// evaluation is explicitly wired into runtime callers.
    /// </summary>
    public enum SectionDomain
    {
        Distance = 0,
        Time = 1
    }
}
