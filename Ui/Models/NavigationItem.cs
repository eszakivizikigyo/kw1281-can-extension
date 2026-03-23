using System.Collections.ObjectModel;

namespace BitFab.KW1281Test.Ui.Models;

public class NavigationItem
{
    public string Title { get; init; } = string.Empty;
    public string? IconGlyph { get; init; }
    public string? Tag { get; init; }
    public ObservableCollection<NavigationItem>? Children { get; init; }
    public bool IsExpanded { get; set; } = true;
}
