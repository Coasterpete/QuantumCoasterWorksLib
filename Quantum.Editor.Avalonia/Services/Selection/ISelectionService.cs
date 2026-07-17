using System.Collections.Generic;

namespace Quantum.Editor.Avalonia.Services.Selection;

public interface ISelectionService
{
    IReadOnlyList<object> SelectedItems { get; }

    void SetSelection(IEnumerable<object> items);

    void Clear();
}
