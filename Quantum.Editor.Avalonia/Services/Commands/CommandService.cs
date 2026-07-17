using System.Collections.Generic;

namespace Quantum.Editor.Avalonia.Services.Commands;

public sealed class CommandService : ICommandService
{
    private readonly Dictionary<string, IEditorCommand> commands = new();

    public void Register(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands[command.Id] = command;
    }

    public bool TryGet(string commandId, out IEditorCommand? command)
    {
        return commands.TryGetValue(commandId, out command);
    }
}
