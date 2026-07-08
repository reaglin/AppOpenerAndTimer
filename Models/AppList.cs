namespace AppOpenerAndTimer.Models;

/// <summary>
/// A named, saved selection of apps plus its launch interval.
/// A blank <see cref="Name"/> means an unsaved (ad-hoc) selection.
/// </summary>
public class AppList
{
    public string Name { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Package names in this list. Launch order is alphabetical by label at run time.</summary>
    public List<string> Packages { get; set; } = new();

    public bool IsSaved => !string.IsNullOrWhiteSpace(Name);

    public AppList Clone() => new()
    {
        Name = Name,
        IntervalSeconds = IntervalSeconds,
        Packages = new List<string>(Packages),
    };
}
