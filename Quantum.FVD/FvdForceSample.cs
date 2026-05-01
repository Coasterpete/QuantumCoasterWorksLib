namespace Quantum.FVD
{
    /// <summary>
    /// Stored force-design sample keyed by normalized station U.
    /// Data-only in v0.04 (no solver coupling yet).
    /// </summary>
    public readonly struct FvdForceSample
    {
        public double U { get; }

        public double NormalG { get; }

        public double LateralG { get; }

        public double RollRateDegPerSec { get; }

        public FvdForceSample(double u, double normalG, double lateralG, double rollRateDegPerSec)
        {
            U = u;
            NormalG = normalG;
            LateralG = lateralG;
            RollRateDegPerSec = rollRateDegPerSec;
        }
    }
}
