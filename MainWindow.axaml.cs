using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HandyTerminal.Models;
using HandyTerminal.Services;

namespace HandyTerminal;

public partial class MainWindow : Window
{
    private readonly List<Category> _categories = new();
    private Process? _activeProcess;
    private int _selectedCategoryIndex;

    public MainWindow()
    {
        InitializeComponent();

        AddTabButton.Click += (_, _) => AddNewTab();
        AddCommandButton.Click += (_, _) => AddNewCommand();
        DeleteTabButton.Click += (_, _) => DeleteSelectedTab();
        ImportButton.Click += async (_, _) => await ImportCommandsAsync();
        ExportButton.Click += async (_, _) => await ExportCommandsAsync();
        SaveTabTitleButton.Click += (_, _) => SaveSelectedTabTitle();
        RunInputButton.Click += (_, _) => _ = RunCommandAsync(CommandInput.Text ?? string.Empty);
        StopButton.Click += (_, _) => StopActiveCommand();
        ClearButton.Click += (_, _) => ClearTerminal();
        KeyDown += HandleWindowKeyDown;
        TabTitleBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SaveSelectedTabTitle();
            }
        };
        CommandInput.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await RunCommandAsync(CommandInput.Text ?? string.Empty);
            }
        };

        LoadCommands();
    }

    private void LoadCommands()
    {
        _categories.Clear();
        _categories.AddRange(CommandService.LoadCommands());

        if (_categories.Count == 0)
        {
            _categories.Add(new Category { Name = "Quick Commands" });
        }

        if (_selectedCategoryIndex >= _categories.Count)
        {
            _selectedCategoryIndex = _categories.Count - 1;
        }

        BuildTabBar();
        BuildCommandEditors();
    }

    private void BuildTabBar()
    {
        TabBar.Children.Clear();

        for (var index = 0; index < _categories.Count; index++)
        {
            var category = _categories[index];
            var isSelected = index == _selectedCategoryIndex;
            var idx = index; // capture loop variable for closures

            var tabButton = new Button
            {
                Content = string.IsNullOrWhiteSpace(category.Name) ? $"Tab {idx + 1}" : category.Name,
                Width = 140,
                Height = 34,
                FontWeight = isSelected ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal
            };

            tabButton.Click += (_, _) =>
            {
                // switch selected tab safely
                if (idx >= 0 && idx < _categories.Count)
                {
                    _selectedCategoryIndex = idx;
                    BuildTabBar();
                    BuildCommandEditors();
                }
            };

            TabBar.Children.Add(tabButton);
        }
    }

    private void BuildCommandEditors()
    {
        CommandList.Children.Clear();

        if (_categories.Count == 0)
        {
            return;
        }

        var category = _categories[Math.Min(_selectedCategoryIndex, _categories.Count - 1)];
        TabTitleBox.Text = category.Name;

        CommandList.Children.Add(new TextBlock
        {
            Text = category.Name,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var command in category.Buttons)
        {
            var currentButton = command;
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var titleBox = new TextBox
            {
                Text = currentButton.Title,
                PlaceholderText = "Title",
                Width = 150
            };

            var commandBox = new TextBox
            {
                Text = currentButton.Command,
                PlaceholderText = "Command",
                MinWidth = 280
            };

            var sudoCheck = new CheckBox
            {
                IsChecked = currentButton.RunAsSudo,
                Content = "Sudo",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var runButton = new Button { Content = "Run", Width = 70 };
            var saveButton = new Button { Content = "Save", Width = 70 };
            var deleteButton = new Button { Content = "Delete", Width = 80 };

            runButton.Click += async (_, _) =>
            {
                var cmd = commandBox.Text ?? string.Empty;
                if ((sudoCheck.IsChecked ?? false) && !cmd.TrimStart().StartsWith("sudo ", StringComparison.OrdinalIgnoreCase))
                {
                    cmd = "sudo " + cmd;
                }

                await RunCommandAsync(cmd);
            };

            saveButton.Click += (_, _) =>
            {
                currentButton.Title = titleBox.Text ?? string.Empty;
                currentButton.Command = commandBox.Text ?? string.Empty;
                currentButton.RunAsSudo = sudoCheck.IsChecked ?? false;
                SaveCommands();
                AppendLine($"Saved: {currentButton.Title}");
            };

            deleteButton.Click += (_, _) =>
            {
                category.Buttons.Remove(currentButton);
                SaveCommands();
                BuildCommandEditors();
            };

            row.Children.Add(titleBox);
            row.Children.Add(commandBox);
            row.Children.Add(sudoCheck);
            row.Children.Add(runButton);
            row.Children.Add(saveButton);
            row.Children.Add(deleteButton);

            CommandList.Children.Add(row);
        }
    }

    private void AddNewTab()
    {
        _categories.Add(new Category { Name = $"Tab {_categories.Count + 1}" });
        _selectedCategoryIndex = _categories.Count - 1;
        SaveCommands();
        BuildTabBar();
        BuildCommandEditors();
    }

    private void AddNewCommand()
    {
        var category = GetSelectedCategory();
        category.Buttons.Add(new CommandButton
        {
            Title = "New command",
            Command = "echo hello"
        });

        SaveCommands();
        BuildCommandEditors();
    }

    private void DeleteSelectedTab()
    {
        try
        {
            if (_categories.Count <= 1)
            {
                AppendLine("Cannot delete the last tab.");
                return;
            }

            if (_selectedCategoryIndex < 0 || _selectedCategoryIndex >= _categories.Count)
            {
                AppendLine("No tab selected to delete.");
                return;
            }

            _categories.RemoveAt(_selectedCategoryIndex);

            if (_selectedCategoryIndex >= _categories.Count)
            {
                _selectedCategoryIndex = _categories.Count - 1;
            }

            SaveCommands();
            BuildTabBar();
            BuildCommandEditors();
        }
        catch (Exception ex)
        {
            // Avoid crashing the app on unexpected errors; report to terminal output for diagnosis.
            AppendLine($"Error deleting tab: {ex.Message}");
        }
    }

    private void SaveSelectedTabTitle()
    {
        var category = GetSelectedCategory();
        category.Name = TabTitleBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            category.Name = $"Tab {_selectedCategoryIndex + 1}";
        }

        SaveCommands();
        BuildTabBar();
        BuildCommandEditors();
        AppendLine($"Saved tab: {category.Name}");
    }

    private Category GetSelectedCategory()
    {
        if (_categories.Count == 0)
        {
            _categories.Add(new Category { Name = "Quick Commands" });
        }

        if (_selectedCategoryIndex < 0 || _selectedCategoryIndex >= _categories.Count)
        {
            _selectedCategoryIndex = 0;
        }

        return _categories[_selectedCategoryIndex];
    }

    private void SaveCommands()
    {
        CommandService.SaveCommands(_categories);
    }

    private async Task ImportCommandsAsync()
    {
        try
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Import command buttons",
                AllowMultiple = false
            };

            var files = await this.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0)
            {
                return;
            }

            var file = files[0];
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var imported = ParseImportedCategories(content);

            if (imported.Count == 0)
            {
                AppendLine("Import failed: no tabs found in the selected file.");
                return;
            }

            // Merge imported categories into existing ones. If a category name matches (case-insensitive),
            // append its buttons to the existing category. Otherwise add as a new tab.
            foreach (var imp in imported)
            {
                if (string.IsNullOrWhiteSpace(imp.Name))
                {
                    // If imported tab has no name, add it as a new unnamed tab
                    _categories.Add(imp);
                    continue;
                }

                var existing = _categories.FirstOrDefault(c => c.Name?.Equals(imp.Name, StringComparison.OrdinalIgnoreCase) == true);
                if (existing is not null)
                {
                    // Append buttons (avoid duplicates by command text + title)
                    foreach (var btn in imp.Buttons)
                    {
                        var duplicate = existing.Buttons.Any(b => string.Equals(b.Command?.Trim(), btn.Command?.Trim(), StringComparison.Ordinal) &&
                                                                  string.Equals(b.Title?.Trim(), btn.Title?.Trim(), StringComparison.Ordinal));
                        if (!duplicate)
                        {
                            existing.Buttons.Add(btn);
                        }
                    }
                }
                else
                {
                    _categories.Add(imp);
                }
            }

            // Ensure a sensible selected tab
            if (_selectedCategoryIndex < 0 || _selectedCategoryIndex >= _categories.Count)
            {
                _selectedCategoryIndex = 0;
            }

            SaveCommands();
            BuildTabBar();
            BuildCommandEditors();
            AppendLine($"Imported and merged from {file.Name}");
        }
        catch (Exception ex)
        {
            AppendLine($"Import error: {ex.Message}");
        }
    }

    private async Task ExportCommandsAsync()
    {
        try
        {
            var options = new FilePickerSaveOptions
            {
                Title = "Export command buttons",
                SuggestedFileName = "commands.txt"
            };

            var file = await this.StorageProvider.SaveFilePickerAsync(options);
            if (file == null)
            {
                return;
            }

            var content = BuildExportText();
            using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
            await writer.FlushAsync();

            AppendLine($"Exported to {file.Name}");
        }
        catch (Exception ex)
        {
            AppendLine($"Export error: {ex.Message}");
        }
    }

    private string BuildExportText()
    {
        var lines = new List<string>();

        foreach (var category in _categories)
        {
            if (lines.Count > 0)
            {
                lines.Add("__________");
            }

            lines.Add(string.IsNullOrWhiteSpace(category.Name) ? "Tab" : category.Name);

            foreach (var button in category.Buttons)
            {
                var commandText = string.IsNullOrWhiteSpace(button.Command)
                    ? button.Title
                    : button.Command;

                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    lines.Add(commandText);
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private List<Category> ParseImportedCategories(string content)
    {
        var categories = new List<Category>();
        var current = new Category();
        var hasCurrent = false;

        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsTabSeparator(line))
            {
                if (hasCurrent)
                {
                    categories.Add(current);
                }

                current = new Category();
                hasCurrent = true;
                continue;
            }

            if (!hasCurrent || (string.IsNullOrWhiteSpace(current.Name) && current.Buttons.Count == 0))
            {
                current.Name = line;
                hasCurrent = true;
                continue;
            }

            current.Buttons.Add(new CommandButton
            {
                Title = line,
                Command = line
            });
        }

        if (hasCurrent)
        {
            categories.Add(current);
        }

        if (categories.Count == 0)
        {
            return new List<Category> { new Category { Name = "Imported" } };
        }

        return categories;
    }

    private static bool IsTabSeparator(string line)
    {
        return line.Length >= 5 && line.All(c => c == '_');
    }

    private async Task RunCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        if (_activeProcess is { HasExited: false })
        {
            AppendLine("Stopping previous command...");
            StopActiveCommand();
        }

        var normalizedCommand = command.Trim();
        AppendLine($"> {normalizedCommand}");

        var startInfo = CreateStartInfo(normalizedCommand);

        try
        {
            var process = new Process { StartInfo = startInfo };
            _activeProcess = process;
            process.Start();

            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    AppendText(args.Data + Environment.NewLine);
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    AppendText(args.Data + Environment.NewLine);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var password = GetSudoPasswordForCommand(normalizedCommand);
            if (!string.IsNullOrWhiteSpace(password))
            {
                await process.StandardInput.WriteLineAsync(password);
                await process.StandardInput.FlushAsync();
            }

            await process.WaitForExitAsync();
            process.CancelOutputRead();
            process.CancelErrorRead();
        }
        catch (Exception ex)
        {
            AppendLine($"Error: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_activeProcess, null))
            {
                _activeProcess = null;
            }
            else if (_activeProcess?.HasExited == true)
            {
                _activeProcess = null;
            }
        }
    }

    private ProcessStartInfo CreateStartInfo(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(BuildShellCommand(command));
        }

        return startInfo;
    }

    private string BuildShellCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return command;
        }

        var sudoPassword = SudoPasswordBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(sudoPassword) && command.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase))
        {
            var commandAfterSudo = command.Substring(5).TrimStart();
            var escapedPassword = EscapeShellArgument(sudoPassword);
            return $"printf '%s\\n' {escapedPassword} | sudo -S -p '' {commandAfterSudo}";
        }

        return command;
    }

    private string? GetSudoPasswordForCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var password = SudoPasswordBox.Text?.Trim();
        return !string.IsNullOrWhiteSpace(password) && command.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase)
            ? password
            : null;
    }

    private static string EscapeShellArgument(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private void HandleWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            StopActiveCommand();
        }
    }

    private void StopActiveCommand()
    {
        if (_activeProcess is null)
        {
            return;
        }

        AppendLine("^C");

        try
        {
            if (!_activeProcess.HasExited)
            {
                _activeProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            AppendLine($"Stop error: {ex.Message}");
        }
        finally
        {
            _activeProcess = null;
        }
    }

    private void ClearTerminal()
    {
        TerminalOutput.Text = string.Empty;
    }

    private void AppendLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            TerminalOutput.Text = (TerminalOutput.Text ?? string.Empty) + text + Environment.NewLine;
            TerminalOutput.CaretIndex = TerminalOutput.Text.Length;
        });
    }

    private void AppendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            TerminalOutput.Text = (TerminalOutput.Text ?? string.Empty) + text;
            TerminalOutput.CaretIndex = TerminalOutput.Text.Length;
        });
    }
}