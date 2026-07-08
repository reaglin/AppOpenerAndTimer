using System.Collections.ObjectModel;
using AppOpenerAndTimer.Models;
using AppOpenerAndTimer.Services;

namespace AppOpenerAndTimer.Pages;

public partial class ListsPage : ContentPage
{
    public ObservableCollection<AppList> Lists { get; } = new();

    public ListsPage()
    {
        InitializeComponent();
        ListsView.ItemsSource = Lists;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ReloadLists();
    }

    void ReloadLists()
    {
        Lists.Clear();
        foreach (var list in AppListStore.GetAll().OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            Lists.Add(list);
    }

    async void OnNewList(object? sender, EventArgs e)
        => await Navigation.PushAsync(new SelectPage(new AppList()));

    async void OnOpenList(object? sender, EventArgs e)
    {
        if (GetList(sender) is { } list)
            await Navigation.PushAsync(new OpenViewPage(list));
    }

    async void OnEditList(object? sender, EventArgs e)
    {
        if (GetList(sender) is { } list)
            await Navigation.PushAsync(new SelectPage(list.Clone()));
    }

    async void OnDeleteList(object? sender, EventArgs e)
    {
        if (GetList(sender) is not { } list)
            return;

        var confirm = await DisplayAlert("Delete list", $"Delete \"{list.Name}\"?", "Delete", "Cancel");
        if (!confirm)
            return;

        AppListStore.Delete(list.Name);
        ReloadLists();
    }

    static AppList? GetList(object? sender)
        => (sender as Button)?.CommandParameter as AppList;
}
