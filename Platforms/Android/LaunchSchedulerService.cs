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
    DateTime _lastPoll;
    DateTime? _idleSince;
    Handler? _handler;

    // Stop the service this long after the user has left every launched app.
    static readonly TimeSpan IdleGrace = TimeSpan.FromSeconds(45);

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
        _lastPoll = DateTime.UtcNow;
        _idleSince = null;

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

    // --- foreground time tracking ---

    void Poll()
    {
        var now = DateTime.UtcNow;
        var deltaMs = (long)(now - _lastPoll).TotalMilliseconds;
        _lastPoll = now;

        var foreground = UsageAccess.LatestForeground() ?? _lastForeground;
        _lastForeground = foreground;

        // Which of our launched apps (if any) is actually on screen right now?
        var currentTracked = foreground != null && ScheduleState.LaunchTimes.ContainsKey(foreground)
            ? foreground
            : null;

        // Add the elapsed slice to that app's on-screen total. Guard against a
        // negative or huge delta (e.g. after the device slept).
        if (currentTracked != null && deltaMs > 0 && deltaMs < 60_000)
        {
            ScheduleState.ForegroundSeen.Add(currentTracked);
            ScheduleState.ForegroundMs.TryGetValue(currentTracked, out var ms);
            ScheduleState.ForegroundMs[currentTracked] = ms + deltaMs;
        }

        ScheduleState.CurrentForeground = currentTracked;
        ScheduleState.RaiseChanged();

        // Once launching is done and the user has been away from all of our apps
        // for the grace period, stop the service.
        if (_scheduleDone)
        {
            if (currentTracked == null)
            {
                _idleSince ??= now;
                if (now - _idleSince >= IdleGrace)
                {
                    UpdateNotification("Timing finished.");
                    StopTracking();
                    return;
                }
            }
            else
            {
                _idleSince = null;
            }
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
