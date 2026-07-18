namespace Quantum.Editor.Avalonia.Services.Viewport;

public sealed class ViewportService : IViewportService
{
    public IViewportSurface? ActiveViewport { get; private set; }

    public void SetActiveViewport(IViewportSurface? viewport)
    {
        ActiveViewport = viewport;
    }
}
