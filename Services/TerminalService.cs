using System;

namespace HandyTerminal.Services;

public class TerminalService
{
    public event Action<string>? CommandRequested;

    public void RunCommand(string command)
    {
        CommandRequested?.Invoke(command);
    }
}