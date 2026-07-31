using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandyTerminal.Services;

namespace HandyTerminal;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        LoadButtons();
    }


    private void LoadButtons()
    {
        var categories = CommandService.LoadCommands();

        foreach (var category in categories)
        {
            var title = new TextBlock
            {
                Text = category.Name,
                FontSize = 20,
                Margin = new Thickness(5)
            };

            ButtonArea.Children.Add(title);


            foreach (var command in category.Buttons)
            {
                var button = new Button
                {
                    Content = command.Title,
                    Margin = new Thickness(5)
                };


                button.Click += (s, e) =>
                {
                    TerminalText.Text = command.Command;
                };


                ButtonArea.Children.Add(button);
            }
        }
    }
}