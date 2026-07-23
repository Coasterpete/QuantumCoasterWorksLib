using System.Threading;

namespace Quantum.Editor.Avalonia.Services.Viewport;

/// <summary>
/// Scoped, opt-in measurement for renderer-neutral editor viewport projection.
/// </summary>
internal sealed class EditorViewportPipelineMeasurement : IDisposable
{
    private static readonly AsyncLocal<EditorViewportPipelineMeasurement?> CurrentValue = new();

    private readonly EditorViewportPipelineMeasurement? previous;
    private bool disposed;

    private EditorViewportPipelineMeasurement()
    {
        previous = CurrentValue.Value;
        CurrentValue.Value = this;
    }

    public int ViewportProjectionBuildCount { get; private set; }

    public TimeSpan ViewportProjectionBuildElapsed { get; private set; }

    public static EditorViewportPipelineMeasurement Begin()
    {
        return new EditorViewportPipelineMeasurement();
    }

    internal static void RecordViewportProjectionBuild(TimeSpan elapsed)
    {
        EditorViewportPipelineMeasurement? current = CurrentValue.Value;
        if (current is null)
        {
            return;
        }

        current.ViewportProjectionBuildCount++;
        current.ViewportProjectionBuildElapsed += elapsed;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (ReferenceEquals(CurrentValue.Value, this))
        {
            CurrentValue.Value = previous;
        }

        disposed = true;
    }
}
