using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.Editor.Avalonia.Services.Selection;
using Quantum.Editor.Avalonia.Services.UndoRedo;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class EditorServiceTests
{
    [Fact]
    public void CommandService_ExecutesRegisteredCommandWhenEnabled()
    {
        var service = new CommandService();
        int executions = 0;
        service.Register(new EditorCommand(
            "test.run",
            _ => executions++,
            parameter => Equals(parameter, "enabled")));

        Assert.False(service.Execute("test.run", "disabled"));
        Assert.True(service.Execute("test.run", "enabled"));
        Assert.Equal(1, executions);
    }

    [Fact]
    public void SelectionService_ReplacesAndClearsSelectionWithNotifications()
    {
        var service = new SelectionService();
        int notifications = 0;
        service.SelectionChanged += (_, _) => notifications++;

        service.SetSelection(new object[] { "section-1", "section-2" });
        service.Clear();

        Assert.Empty(service.SelectedItems);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void UndoRedoService_ExecutesUndoAndRedoInOrder()
    {
        var service = new UndoRedoService();
        int value = 0;
        var operation = new TestOperation("Increment", () => value++, () => value--);

        service.Execute(operation);
        Assert.Equal(1, value);
        Assert.Equal("Increment", service.UndoDescription);
        Assert.True(service.Undo());
        Assert.Equal(0, value);
        Assert.Equal("Increment", service.RedoDescription);
        Assert.True(service.Redo());
        Assert.Equal(1, value);
    }

    [Fact]
    public void NewOperationAfterUndo_ClearsRedoHistory()
    {
        var service = new UndoRedoService();
        int value = 0;
        service.Execute(new TestOperation("First", () => value++, () => value--));
        Assert.True(service.Undo());

        service.Execute(new TestOperation("Second", () => value += 2, () => value -= 2));

        Assert.False(service.CanRedo);
        Assert.Equal(2, value);
    }

    [Fact]
    public void DocumentService_TracksOpenAndActiveDocuments()
    {
        var service = new DocumentService();
        var first = new TrackEditorDocument(new TrackDocument(), "First");
        var second = new TrackEditorDocument(new TrackDocument(), "Second");

        service.SetActiveDocument(first);
        service.SetActiveDocument(second);
        service.CloseDocument(second);

        Assert.Single(service.OpenDocuments);
        Assert.Same(first, service.ActiveDocument);
    }

    private sealed class TestOperation : IUndoableEditorOperation
    {
        private readonly Action execute;
        private readonly Action undo;

        public TestOperation(string description, Action execute, Action undo)
        {
            Description = description;
            this.execute = execute;
            this.undo = undo;
        }

        public string Description { get; }

        public void Execute() => execute();

        public void Undo() => undo();
    }
}
