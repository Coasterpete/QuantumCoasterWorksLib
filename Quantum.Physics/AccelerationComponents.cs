namespace Quantum.Physics
{
    /// <summary>
    /// Scalar acceleration components resolved into the local track frame basis.
    /// </summary>
    public readonly struct AccelerationComponents
    {
        public AccelerationComponents(double tangential, double normal, double binormal)
        {
            Tangential = tangential;
            Normal = normal;
            Binormal = binormal;
        }

        public double Tangential { get; }

        public double Normal { get; }

        public double Binormal { get; }
    }
}
