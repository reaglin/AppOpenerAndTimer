using System.Collections.ObjectModel;
using System.ComponentModel;
using Android.Content;
using Android.OS;
using AppOpenerAndTimer.Models;
using AppOpenerAndTimer.Services;
using Application = Android.App.Application;

namespace AppOpenerAndTimer;

public partial class MainPage : ContentPage
{
    const string SelectionKey = "selected_packages";

    public ObservableCollection<AppEntry> Apps { get; } = new();

    readonly IDispatcherTimer _uiTimer;
    bool _suppressSave;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromSeconds(1);
        _uiTimer.Tick += (_, _) => RefreshTimers();

        ScheduleState.Changed += OnScheduleChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RequestNotificationPermission();
        UpdateOverlayWarning();

        if (Apps.Count == 0)
            await LoadAppsAsync();

        _uiTimer.Start();
        RefreshTimers();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _uiTimer.Stop();
    }

    // ---- loading + persistence ----

    async Task LoadAppsAsync()
    {
        StatusLabel.Text = "Loading installed apps…";
        var saved = LoadSavedSelection();

        var apps = await Task.Run(AppLauncher.GetLaunchableApps);

        _suppressSave = true;
        Apps.Clear();
        foreach (var (package, label) in apps)
        {
            var entry = new AppEntry
            {
                PackageName = package,
                Label = label,
                IsSelected = saved.Contains(package),
            };
            entry.PropertyChanged += OnEntryChanged;
            Apps.Add(entry);
        }
        _suppressSave = false;

        StatusLabel.Text = $"{Apps.Count} apps found, {saved.Count} selected.";
    }

    static HashSet<string> LoadSavedSelection()
    {
        var raw = Preferences.Get(SelectionKey, string.Empty);
        return string.IsNullOrEmpty(raw)
            ? new HashSet<string>()
            : new HashSet<string>(raw.Split('|', StringSplitOptions.RemoveEmptyEntries));
    }

    void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppEntry.IsSelected))
            SaveSelection();
    }

    void SaveSelection()
    {
        if (_suppressSave)
            return;

        var selected = Apps.Where(a => a.IsSelected).Select(a => a.PackageName);
        Preferences.Set(SelectionKey, string.Join('|', selected));
    }

    // ---- timers ----

    void OnScheduleChanged() => MainThread.BeginInvokeOnMainThread(RefreshTimers);

    void RefreshTimers()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in Apps)
        {
            if (ScheduleState.LaunchTimes.TryGetValue(entry.PackageName, out var launchedAt))
            {
                if (ScheduleState.ClosedTimes.TryGetValue(entry.PackageName, out var closedAt))
                {
                    entry.ElapsedText = Format(closedAt - launchedAt);
                    entry.Status = "Closed";
                }
                else
                {
                    entry.ElapsedText = Format(now - launchedAt);
                    entry.Status = ScheduleState.ForegroundSeen.Contains(entry.PackageName)
                        ? "Open"
                        : "Opening…";
                }
            }
            else if (entry.IsSelected && ScheduleState.IsRunning)
            {
                entry.Status = "Waiting to launch…";
                entry.ElapsedText = "--:--";
            }
            else
            {
                entry.Status = entry.IsSelected ? "Queued" : "Not launched";
                entry.ElapsedText = "00:00";
            }
        }

        var launched = ScheduleState.LaunchTimes.Count;
        var closed = ScheduleState.ClosedTimes.Count;
        if (ScheduleState.IsRunning)
            StatusLabel.Text = $"Running… {launched} launched so far.";
        else if (launched > 0)
            StatusLabel.Text = closed >= launched
                ? "Done. All apps closed; final times shown."
                : $"Timing… {closed}/{launched} closed. Timers stop as you close each app.";
    }

    static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    // ---- buttons ----

    async void OnGo(object? sender, EventArgs e)
    {
        var selected = Apps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await DisplayAlert("Nothing selected", "Tick at least one app first.", "OK");
            return;
        }

        if (!AppLauncher.CanDrawOverlays())
        {
            UpdateOverlayWarning();
            var go = await DisplayAlert(
                "Permission needed",
                "To launch apps while this one is in the background, allow 'Display over other apps'. Open settings now?",
                "Open settings", "Cancel");
            if (go)
                OpenOverlaySettings();
            return;
        }

        if (!UsageAccess.IsGranted())
        {
            var grant = await DisplayAlert(
                "Usage access (optional)",
                "To stop each app's timer the moment you close it, grant 'Usage access'. Without it, timers just keep counting from launch. Open settings now?",
                "Open settings", "Skip");
            if (grant)
            {
                UsageAccess.OpenSettings();
                return;
            }
        }

        var interval = 60;
        if (int.TryParse(IntervalEntry.Text, out var parsed) && parsed > 0)
            interval = parsed;

        var ctx = Application.Context;
        var intent = new Intent(ctx, typeof(LaunchSchedulerService));
        intent.PutExtra(LaunchSchedulerService.ExtraPackages, selected.Select(a => a.PackageName).ToArray());
        intent.PutExtra(LaunchSchedulerService.ExtraLabels, selected.Select(a => a.Label).ToArray());
        intent.PutExtra(LaunchSchedulerService.ExtraInterval, interval);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            ctx.StartForegroundService(intent);
        else
            ctx.StartService(intent);

        StatusLabel.Text = $"Launching {selected.Count} apps, {interval}s apart…";
    }

    void OnStop(object? sender, EventArgs e)
    {
        var ctx = Application.Context;
        var intent = new Intent(ctx, typeof(LaunchSchedulerService));
        intent.SetAction(LaunchSchedulerService.ActionStop);
        ctx.StartService(intent);
        StatusLabel.Text = "Stopped. Remaining apps will not launch.";
    }

    void OnReset(object? sender, EventArgs e)
    {
        ScheduleState.Reset();
        RefreshTimers();
        StatusLabel.Text = "Timers reset.";
    }

    async void OnRefresh(object? sender, EventArgs e) => await LoadAppsAsync();

    void OnGrantOverlay(object? sender, EventArgs e) => OpenOverlaySettings();

    // ---- permissions ----

    void UpdateOverlayWarning() => OverlayWarning.IsVisible = !AppLauncher.CanDrawOverlays();

    void OpenOverlaySettings()
    {
        var ctx = Application.Context;
        var intent = new Intent(
            Android.Provider.Settings.ActionManageOverlayPermission,
            Android.Net.Uri.Parse("package:" + ctx.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
    }

    static void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return;

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
            return;

        if (activity.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Android.Content.PM.Permission.Granted)
            activity.RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, 100);
    }
}
