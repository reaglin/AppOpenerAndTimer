using System.Collections.ObjectModel;
using AppOpenerAndTimer.Models;
using AppOpenerAndTimer.Services;

namespace AppOpenerAndTimer.Pages;

public partial class SelectPage : ContentPage
{
    AppList _list;
    public ObservableCollection<AppEntry> Apps { get; } = new();

    public SelectPage(AppList list)
    {
        InitializeComponent();
        _list = list;
        HeaderLabel.Text = list.IsSaved ? $"Edit: {list.Name}" : "New list";
        IntervalEntry.Text = list.IntervalSeconds.ToString();
        AppsView.ItemsSource = Apps;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Apps.Count == 0)
            await LoadAppsAsync();
    }

    async Task LoadAppsAsync()
    {
        var apps = await Task.Run(AppLauncher.GetLaunchableApps);
        var selected = new HashSet<string>(_list.Packages);

        Apps.Clear();
        foreach (var (package, label) in apps)
        {
            Apps.Add(new AppEntry
            {
                PackageName = package,
                Label = label,
                IsSelected = selected.Contains(package),
            });
        }
    }

    List<string> SelectedPackages()
        => Apps.Where(a => a.IsSelected).Select(a => a.PackageName).ToList();

    int ParsedInterval()
        => int.TryParse(IntervalEntry.Text, out var v) && v > 0 ? v : 60;

    /// <summary>Pulls current UI state into the working list. Returns false if nothing is selected.</summary>
    async Task<bool> CaptureSelectionAsync()
    {
        var packages = SelectedPackages();
        if (packages.Count == 0)
        {
            await DisplayAlert("Nothing selected", "Tick at least one app first.", "OK");
            return false;
        }

        _list.Packages = packages;
        _list.IntervalSeconds = ParsedInterval();
        return true;
    }

    async void OnSave(object? sender, EventArgs e)
    {
        if (!await CaptureSelectionAsync())
            return;

        if (!_list.IsSaved)
        {
            var name = await DisplayPromptAsync("Save list", "Name this list:", "Save", "Cancel",
                placeholder: "e.g. Morning routine");
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();
            if (AppListStore.Exists(name))
            {
                var overwrite = await DisplayAlert("Name in use",
                    $"A list called \"{name}\" already exists. Overwrite it?", "Overwrite", "Cancel");
                if (!overwrite)
                    return;
            }
            _list.Name = name;
            HeaderLabel.Text = $"Edit: {name}";
        }

        AppListStore.Save(_list);
        await DisplayAlert("Saved", $"\"{_list.Name}\" saved.", "OK");
    }

    async void OnStart(object? sender, EventArgs e)
    {
        if (!await CaptureSelectionAsync())
            return;

        await Navigation.PushAsync(new TimerPage(_list.Clone(), autoStart: false));
    }
}
