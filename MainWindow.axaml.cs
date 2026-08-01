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
using Avalonia.Threading;
using HandyTerminal.Models;
using HandyTerminal.Services;

namespace HandyTerminal;

public partial class MainWindow : Window
{
    private readonly List<Category> _categories = new();
    private Process? _activeProcess;

    public MainWindow()
    {
        InitializeComponent();

        AddCommandButton.Click += (_, _) => AddNewCommand();
        RunInputButton.Click += (_, _) => _ = RunCommandAsync(CommandInput.Text ?? string.Empty);
        StopButton.Click += (_, _) => StopActiveCommand();
        ClearButton.Click += (_, _) => ClearTerminal();
        KeyDown += HandleWindowKeyDown;
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

        BuildCommandEditors();
    }

    private void BuildCommandEditors()
    {
        CommandList.Children.Clear();

        foreach (var category in _categories)
        {
            CommandList.Children.Add(new TextBlock
            {
                Text = category.Name,
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 4)
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
    }

    private void AddNewCommand()
    {
        var category = _categories.FirstOrDefault(c => c.Name == "Quick Commands") ?? _categories.FirstOrDefault();

        if (category is null)
        {
            category = new Category { Name = "Quick Commands" };
            _categories.Add(category);
        }

        category.Buttons.Add(new CommandButton
        {
            Title = "New command",
            Command = "echo hello"
        });

        SaveCommands();
        BuildCommandEditors();
    }

    private void SaveCommands()
    {
        CommandService.SaveCommands(_categories);
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