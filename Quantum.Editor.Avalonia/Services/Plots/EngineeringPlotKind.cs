namespace Quantum.Editor.Avalonia.Services.Plots;

[Flags]
public enum EngineeringPlotKind
{
    None = 0,
    Elevation = 1 << 0,
    Curvature = 1 << 1,
    Roll = 1 << 2,
    Pitch = 1 << 3,
    Yaw = 1 << 4,
    All = Elevation | Curvature | Roll | Pitch | Yaw
}
