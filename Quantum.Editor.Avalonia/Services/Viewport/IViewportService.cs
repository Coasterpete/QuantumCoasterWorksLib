namespace Quantum.Editor.Avalonia.Services.Viewport;

public interface IViewportService
{
    IViewportSurface? ActiveViewport { get; }

    void SetActiveViewport(IViewportSurface? viewport);
}
