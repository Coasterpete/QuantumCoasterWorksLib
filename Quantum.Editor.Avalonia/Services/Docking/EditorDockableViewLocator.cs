using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;

namespace Quantum.Editor.Avalonia.Services.Docking;

/// <summary>
/// Presents the reusable frontend control registered as a dockable context.
/// </summary>
public sealed class EditorDockableViewLocator : IDataTemplate
{
    public Control? Build(object? data) =>
        data is IDockable { Context: Control control }
            ? new EditorDockableContentHost(control)
            : null;

    public bool Match(object? data) =>
        data is IDockable { Context: Control };
}

internal sealed class EditorDockableContentHost : ContentControl
{
    private readonly Control pane;

    internal EditorDockableContentHost(Control pane)
    {
        this.pane = pane ?? throw new ArgumentNullException(nameof(pane));
        HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        if (this.GetVisualAncestors().Any(ancestor => ancestor is DockableControl))
        {
            Content = pane;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        if (ReferenceEquals(Content, pane))
        {
            Content = null;
        }

        base.OnDetachedFromVisualTree(eventArgs);
    }
}
