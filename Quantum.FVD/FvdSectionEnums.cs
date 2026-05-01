namespace Quantum.FVD
{
    public enum FvdFunctionDomain
    {
        Distance,
        Time
    }

    public enum FvdSectionKind
    {
        Force,
        Geometry
    }

    public enum FvdSectionChannel
    {
        NormalG,
        LateralG,
        RollRateDegPerSec,
        Curvature,
        RollAngleDeg
    }
}
