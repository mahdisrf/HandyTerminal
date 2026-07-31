using System.Collections.Generic;

namespace HandyTerminal.Models;

public class Category
{
    public string Name { get; set; } = "";

    public List<CommandButton> Buttons { get; set; } = new();
}