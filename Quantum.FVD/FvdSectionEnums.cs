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

    [System.Flags]
    public enum FvdForceTargetDiagnostic
    {
        None = 0,
        NoForceSection = 1 << 0,
        MissingNormalG = 1 << 1,
        MissingLateralG = 1 << 2,
        MissingRollRateDegPerSec = 1 << 3
    }
}
