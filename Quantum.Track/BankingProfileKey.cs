namespace Quantum.Track
{
    /// <summary>
    /// Authored roll value at a station distance in a BankingProfile.
    /// </summary>
    public readonly struct BankingProfileKey
    {
        public BankingProfileKey(
            double distance,
            double rollRadians,
            BankingProfileInterpolationMode interpolationToNext = BankingProfileInterpolationMode.Constant)
        {
            Distance = distance;
            RollRadians = rollRadians;
            InterpolationToNext = interpolationToNext;
        }

        public double Distance { get; }

        public double RollRadians { get; }

        public BankingProfileInterpolationMode InterpolationToNext { get; }
    }
}
