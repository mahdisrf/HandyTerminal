using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HandyTerminal.Models;

namespace HandyTerminal.Services;

public static class CommandService
{
    public static List<Category> LoadCommands()
    {
        var file = GetCommandFilePath();

        if (!File.Exists(file))
        {
            return new List<Category>();
        }

        var json = File.ReadAllText(file);

        return JsonSerializer.Deserialize<List<Category>>(json)
               ?? new List<Category>();
    }

    public static void SaveCommands(List<Category> categories)
    {
        var file = GetCommandFilePath();
        var directory = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(categories, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(file, json);
    }

    private static string GetCommandFilePath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "commands.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "commands.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }
}
