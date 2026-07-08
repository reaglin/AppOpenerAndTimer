using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AppOpenerAndTimer.Services;

namespace AppOpenerAndTimer;

/// <summary>
/// Foreground service that launches the selected apps one at a time, waiting a
/// fixed interval between each, then keeps polling the foreground app so it can
/// freeze each app's timer the moment that app is closed. It stops itself once
/// every launched app has been closed.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class LaunchSchedulerService : Service
{
    public const string ExtraPackages = "packages";
    public const string ExtraLabels = "labels";
    public const string ExtraInterval = "interval";
    public const string ActionStop = "com.eaglin.appopenerandtimer.STOP";

    const int NotificationId = 4711;
    const string ChannelId = "app_opener_schedule";
    const int PollIntervalMs = 1000;

    string[] _packages = Array.Empty<string>();
    string[] _labels = Array.Empty<string>();
    int _intervalMs = 60_000;
    int _index;
    bool _scheduleDone;
    string? _lastForeground;
    Handler? _handler;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopEverything();
            return StartCommandResult.NotSticky;
        }

        _packages = intent?.GetStringArrayExtra(ExtraPackages) ?? Array.Empty<string>();
        _labels = intent?.GetStringArrayExtra(ExtraLabels) ?? _packages;
        var intervalSeconds = intent?.GetIntExtra(ExtraInterval, 60) ?? 60;
        _intervalMs = Math.Max(1, intervalSeconds) * 1000;
        _index = 0;
        _scheduleDone = false;
        _lastForeground = null;

        CreateChannel();
        StartInForeground("Starting…");

        ScheduleState.Reset();
        ScheduleState.IsRunning = true;
        ScheduleState.RaiseChanged();

        _handler = new Handler(Looper.MainLooper!);

        if (_packages.Length == 0)
        {
            Finish();
        }
        else
        {
            LaunchNext();
            _handler.PostDelayed(Poll, PollIntervalMs);
        }

        return StartCommandResult.NotSticky;
    }

    // --- launch schedule ---

    void LaunchNext()
    {
        if (_index >= _packages.Length)
        {
            Finish();
            return;
        }

        var pkg = _packages[_index];
        var label = _index < _labels.Length ? _labels[_index] : pkg;

        AppLauncher.Launch(pkg);
        ScheduleState.LaunchTimes[pkg] = DateTime.UtcNow;
        ScheduleState.CurrentPackage = pkg;
        ScheduleState.RaiseChanged();

        UpdateNotification($"Launched {label} ({_index + 1}/{_packages.Length})");
        _index++;

        if (_index < _packages.Length)
            _handler?.PostDelayed(LaunchNext, _intervalMs);
        else
            _handler?.PostDelayed(Finish, 750);
    }

    void Finish()
    {
        _scheduleDone = true;
        ScheduleState.IsRunning = false;
        ScheduleState.CurrentPackage = null;
        ScheduleState.RaiseChanged();
        UpdateNotification("All apps launched. Timing until you close each one…");
    }

    // --- foreground / close tracking ---

    void Poll()
    {
        var foreground = UsageAccess.LatestForeground() ?? _lastForeground;
        _lastForeground = foreground;

        var changed = false;
        var self = PackageName;

        foreach (var pkg in ScheduleState.LaunchTimes.Keys.ToList())
        {
            if (ScheduleState.ClosedTimes.ContainsKey(pkg))
                continue;

            if (pkg == foreground)
            {
                // This app is on screen now.
                if (ScheduleState.ForegroundSeen.Add(pkg))
                    changed = true;
            }
            else if (ScheduleState.ForegroundSeen.Contains(pkg) && foreground != self)
            {
                // It was open and is no longer on screen (and we didn't just
                // pop back into our own app) -> treat it as closed. Freeze it.
                ScheduleState.ClosedTimes[pkg] = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
            ScheduleState.RaiseChanged();

        var everythingClosed = ScheduleState.LaunchTimes.Count > 0
            && ScheduleState.LaunchTimes.Keys.All(k => ScheduleState.ClosedTimes.ContainsKey(k));

        if (_scheduleDone && everythingClosed)
        {
            UpdateNotification("All apps closed. Tracking finished.");
            StopTracking();
            return;
        }

        _handler?.PostDelayed(Poll, PollIntervalMs);
    }

    // --- teardown ---

    void StopTracking()
    {
        _handler?.RemoveCallbacksAndMessages(null);
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    void StopEverything()
    {
        _handler?.RemoveCallbacksAndMessages(null);
        ScheduleState.IsRunning = false;
        ScheduleState.CurrentPackage = null;
        ScheduleState.RaiseChanged();
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    public override void OnDestroy()
    {
        _handler?.RemoveCallbacksAndMessages(null);
        base.OnDestroy();
    }

    // --- notification plumbing ---

    void CreateChannel()
    {
        var mgr = (NotificationManager)GetSystemService(NotificationService)!;
        if (mgr.GetNotificationChannel(ChannelId) is null)
        {
            var channel = new NotificationChannel(ChannelId, "Launch schedule", NotificationImportance.Low)
            {
                Description = "Progress of the app launch schedule."
            };
            mgr.CreateNotificationChannel(channel);
        }
    }

    Notification BuildNotification(string text)
    {
        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("App Opener and Timer")
            .SetContentText(text)
            .SetSmallIcon(Android.Resource.Drawable.IcMenuManage)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .Build()!;
    }

    void StartInForeground(string text)
    {
        var notification = BuildNotification(text);
        var fgsType = Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake
            ? (int)ForegroundService.TypeSpecialUse
            : 0;
        ServiceCompat.StartForeground(this, NotificationId, notification, fgsType);
    }

    void UpdateNotification(string text)
    {
        var mgr = (NotificationManager)GetSystemService(NotificationService)!;
        mgr.Notify(NotificationId, BuildNotification(text));
    }
}
