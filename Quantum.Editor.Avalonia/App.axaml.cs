using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using Quantum.Editor.Avalonia.Services;

namespace Quantum.Editor.Avalonia;

public partial class App : global::Avalonia.Application
{
    public EditorWorkspace? Workspace { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Workspace = new EditorWorkspace();
            Workspace.NewDocument();
            desktop.MainWindow = new MainWindow(Workspace);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
