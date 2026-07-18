using System.Collections.Generic;

namespace Quantum.Editor.Avalonia.Services.Selection;

public sealed class SelectionService : ISelectionService
{
    private readonly List<object> selectedItems = new();

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<object> SelectedItems => selectedItems;

    public void SetSelection(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        selectedItems.Clear();
        selectedItems.AddRange(items);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        selectedItems.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
