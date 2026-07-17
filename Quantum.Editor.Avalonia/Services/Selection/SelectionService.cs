using System.Collections.Generic;

namespace Quantum.Editor.Avalonia.Services.Selection;

public sealed class SelectionService : ISelectionService
{
    private readonly List<object> selectedItems = new();

    public IReadOnlyList<object> SelectedItems => selectedItems;

    public void SetSelection(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        selectedItems.Clear();
        selectedItems.AddRange(items);
    }

    public void Clear()
    {
        selectedItems.Clear();
    }
}
