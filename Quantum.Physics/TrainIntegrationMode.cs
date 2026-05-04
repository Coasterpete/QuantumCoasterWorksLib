namespace Quantum.Physics
{
    /// <summary>
    /// Selects the train integration strategy used by <see cref="TrainStepLoop"/>.
    /// </summary>
    public enum TrainIntegrationMode
    {
        LegacyNormalComponent = 0,
        TangentialProjected = 1
    }
}
