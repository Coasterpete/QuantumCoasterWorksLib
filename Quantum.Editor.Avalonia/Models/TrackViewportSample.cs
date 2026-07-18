using Quantum.Math;

namespace Quantum.Editor.Avalonia.Models;

public readonly record struct TrackViewportSample(
    int SampleIndex,
    int SectionIndex,
    double Distance,
    Vector3d Position,
    Vector3d Tangent,
    Vector3d Normal,
    Vector3d Binormal,
    double Curvature,
    double RollDegrees);
