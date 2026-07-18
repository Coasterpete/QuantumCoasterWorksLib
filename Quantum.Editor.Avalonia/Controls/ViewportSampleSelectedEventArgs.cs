using Quantum.Editor.Avalonia.Models;

namespace Quantum.Editor.Avalonia.Controls;

public sealed class ViewportSampleSelectedEventArgs : EventArgs
{
    public ViewportSampleSelectedEventArgs(TrackViewportSample sample)
    {
        Sample = sample;
    }

    public TrackViewportSample Sample { get; }
}
