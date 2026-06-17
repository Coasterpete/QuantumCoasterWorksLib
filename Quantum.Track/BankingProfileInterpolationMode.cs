namespace Quantum.Track
{
    /// <summary>
    /// Interpolation policy for a BankingProfile key interval.
    /// </summary>
    public enum BankingProfileInterpolationMode
    {
        Constant = 0,
        Linear = 1,
        SmoothStep = 2,
        Quadratic = 3,
        Cubic = 4,
        Quartic = 5,
        Quintic = 6,
        Sinusoidal = 7
    }
}
