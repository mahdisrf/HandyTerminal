using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using HandyTerminal.Models;


namespace HandyTerminal.Services;

public static class CommandService
{
    public static List<Category> LoadCommands()
    {
        string file = "Data/commands.json";

        if (!File.Exists(file))
            return new List<Category>();

        string json = File.ReadAllText(file);

        return JsonSerializer.Deserialize<List<Category>>(json)
               ?? new List<Category>();
    }
}