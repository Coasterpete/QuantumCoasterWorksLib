namespace Quantum.Editor.Avalonia.Controls;

public sealed class EngineeringStationChangedEventArgs : EventArgs
{
    public EngineeringStationChangedEventArgs(int sampleIndex, double station)
    {
        SampleIndex = sampleIndex;
        Station = station;
    }

    public int SampleIndex { get; }

    public double Station { get; }
}
