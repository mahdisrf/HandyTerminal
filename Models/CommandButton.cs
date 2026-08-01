namespace HandyTerminal.Models;

public class CommandButton
{
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    // When true, run the command prefixed with sudo and prompt for password (use the Sudo password box at the bottom)
    public bool RunAsSudo { get; set; } = false;
}