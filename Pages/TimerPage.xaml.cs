using System.Collections.ObjectModel;
using Android.Content;
using Android.OS;
using AppOpenerAndTimer.Models;
using AppOpenerAndTimer.Services;
using Application = Android.App.Application;

namespace AppOpenerAndTimer.Pages;

public partial class TimerPage : ContentPage
{
    readonly AppList _list;
    readonly bool _autoStart;
    readonly ObservableCollection<AppEntry> _apps = new();
    readonly IDispatcherTimer _uiTimer;
    bool _loaded;
    bool _autoStartTried;

    public TimerPage(AppList list, bool autoStart)
    {
        InitializeComponent();
        _list = list;
        _autoStart = autoStart;
        HeaderLabel.Text = list.IsSaved ? list.Name : "Timers";
        SubtitleLabel.Text = $"{list.Packages.Count} apps · {list.IntervalSeconds}s apart";
        AppsView.ItemsSource = _apps;

        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromSeconds(1);
        _uiTimer.Tick += (_, _) => RefreshTimers();

        ScheduleState.Changed += OnScheduleChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateOverlayWarning();

        if (!_loaded)
        {
            await LoadAppsAsync();
            _loaded = true;
        }

        _uiTimer.Start();
        RefreshTimers();

        if (_autoStart && !_autoStartTried)
        {
            _autoStartTried = true;
            await TryStartAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _uiTimer.Stop();
    }

    async Task LoadAppsAsync()
    {
        var map = await Task.Run(AppLauncher.GetLabelMap);
        var ordered = _list.Packages
            .Select(p => (Package: p, Label: map.TryGetValue(p, out var l) ? l : p))
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _apps.Clear();
        foreach (var (package, label) in ordered)
            _apps.Add(new AppEntry { PackageName = package, Label = label });
    }

    // ---- timers ----

    void OnScheduleChanged() => MainThread.BeginInvokeOnMainThread(RefreshTimers);

    void RefreshTimers()
    {
        foreach (var entry in _apps)
        {
            var launched = ScheduleState.LaunchTimes.ContainsKey(entry.PackageName);
            if (launched)
            {
                entry.ElapsedText = Format(TimeSpan.FromMilliseconds(ScheduleState.ForegroundMillis(entry.PackageName)));
                if (entry.PackageName == ScheduleState.CurrentForeground)
                    entry.Status = "Open (on screen)";
                else if (ScheduleState.ForegroundSeen.Contains(entry.PackageName))
                    entry.Status = "Off screen";
                else
                    entry.Status = "Opening…";
            }
            else if (ScheduleState.IsRunning)
            {
                entry.Status = "Waiting to launch…";
                entry.ElapsedText = "--:--";
            }
            else
            {
                entry.Status = "Not launched";
                entry.ElapsedText = "00:00";
            }
        }

        UpdateRunState();

        var launchedCount = _apps.Count(a => ScheduleState.LaunchTimes.ContainsKey(a.PackageName));
        if (ScheduleState.IsRunning)
            StatusLabel.Text = $"Launching… {launchedCount} started so far.";
        else if (launchedCount > 0)
            StatusLabel.Text = "Timers show how long each app has been on screen.";
    }

    void UpdateRunState()
    {
        var running = ScheduleState.IsRunning || IsSchedulerServiceRunning();
        RunStateLabel.Text = running ? "● Running" : "● Stopped";
        RunStateLabel.TextColor = running ? Colors.SeaGreen : Colors.Gray;
    }

    static bool IsSchedulerServiceRunning()
    {
        // The service keeps timing (and stays alive) after launching finishes,
        // so "running" means the service is still up, not just mid-launch.
        var am = (Android.App.ActivityManager)Application.Context
            .GetSystemService(Context.ActivityService)!;
#pragma warning disable CA1422 // GetRunningServices still reports our own services
        foreach (var svc in am.GetRunningServices(int.MaxValue)!)
        {
            if (svc.Service?.ClassName?.Contains(nameof(LaunchSchedulerService)) == true)
                return true;
        }
#pragma warning restore CA1422
        return false;
    }

    static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    // ---- run controls ----

    async void OnGo(object? sender, EventArgs e) => await TryStartAsync();

    async Task TryStartAsync()
    {
        if (_apps.Count == 0)
        {
            await DisplayAlert("Empty list", "This list has no apps.", "OK");
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
                "To stop each app's timer the moment you close it, grant 'Usage access'. Without it, timers keep counting from launch. Open settings now?",
                "Open settings", "Skip");
            if (grant)
            {
                UsageAccess.OpenSettings();
                return;
            }
        }

        // Launch order is alphabetical (apps are already sorted by label).
        var packages = _apps.Select(a => a.PackageName).ToArray();
        var labels = _apps.Select(a => a.Label).ToArray();

        var ctx = Application.Context;
        var intent = new Intent(ctx, typeof(LaunchSchedulerService));
        intent.PutExtra(LaunchSchedulerService.ExtraPackages, packages);
        intent.PutExtra(LaunchSchedulerService.ExtraLabels, labels);
        intent.PutExtra(LaunchSchedulerService.ExtraInterval, _list.IntervalSeconds);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            ctx.StartForegroundService(intent);
        else
            ctx.StartService(intent);

        StatusLabel.Text = $"Launching {packages.Length} apps, {_list.IntervalSeconds}s apart…";
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

    void OnGrantOverlay(object? sender, EventArgs e) => OpenOverlaySettings();

    void UpdateOverlayWarning() => OverlayWarning.IsVisible = !AppLauncher.CanDrawOverlays();

    static void OpenOverlaySettings()
    {
        var ctx = Application.Context;
        var intent = new Intent(
            Android.Provider.Settings.ActionManageOverlayPermission,
            Android.Net.Uri.Parse("package:" + ctx.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
    }
}
