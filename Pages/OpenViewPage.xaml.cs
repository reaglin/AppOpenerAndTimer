using System.Collections.ObjectModel;
using AppOpenerAndTimer.Models;
using AppOpenerAndTimer.Services;

namespace AppOpenerAndTimer.Pages;

public partial class OpenViewPage : ContentPage
{
    readonly AppList _list;
    readonly ObservableCollection<string> _appLabels = new();

    public OpenViewPage(AppList list)
    {
        InitializeComponent();
        _list = list;
        Title = list.Name;
        NameLabel.Text = list.Name;
        SubtitleLabel.Text = $"{list.Packages.Count} apps · {list.IntervalSeconds}s apart";
        AppsView.ItemsSource = _appLabels;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLabelsAsync();
    }

    async Task LoadLabelsAsync()
    {
        var map = await Task.Run(AppLauncher.GetLabelMap);
        var labels = _list.Packages
            .Select(p => map.TryGetValue(p, out var label) ? label : p)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _appLabels.Clear();
        foreach (var label in labels)
            _appLabels.Add(label);
    }

    async void OnEdit(object? sender, EventArgs e)
        => await Navigation.PushAsync(new SelectPage(_list.Clone()));

    async void OnGo(object? sender, EventArgs e)
        => await Navigation.PushAsync(new TimerPage(_list.Clone(), autoStart: true));
}
