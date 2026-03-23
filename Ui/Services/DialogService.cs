using System.Threading.Tasks;
using Avalonia.Controls;

namespace BitFab.KW1281Test.Ui.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}

public class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var result = false;

        var yesBtn = new Button { Content = "Yes", Width = 80, Classes = { "accent" } };
        var noBtn = new Button { Content = "No", Width = 80 };

        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(0, 16, 0, 0),
                    Children = { yesBtn, noBtn }
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                }
            }
        };

        await dialog.ShowDialog(_owner);
        return result;
    }
}
