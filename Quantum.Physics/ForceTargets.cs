namespace Quantum.Physics
{
    /// <summary>
    /// Immutable force target payload at a given track position.
    /// </summary>
    public readonly struct ForceTargets
    {
        public ForceTargets(double normalG, double lateralG, double rollRateDegPerSec)
        {
            NormalG = normalG;
            LateralG = lateralG;
            RollRateDegPerSec = rollRateDegPerSec;
        }

        public double NormalG { get; }

        public double LateralG { get; }

        public double RollRateDegPerSec { get; }
    }
}
