namespace AppOpenerAndTimer.Services;

/// <summary>
/// Process-wide state shared between the foreground service (which owns the
/// launch schedule and close detection) and the UI (which reads it to render
/// timers).
///
/// Each app's timer is simply (stopped-at ?? now) - launched-at. An app is
/// "stopped" once it has been off screen past a grace window; it resumes if the
/// user reopens it.
/// </summary>
public static class ScheduleState
{
    /// <summary>True while the scheduler service is still launching apps.</summary>
    public static bool IsRunning { get; set; }

    /// <summary>Package name -> UTC time it was launched.</summary>
    public static readonly Dictionary<string, DateTime> LaunchTimes = new();

    /// <summary>Package name -> the last UTC time it was seen on screen.</summary>
    public static readonly Dictionary<string, DateTime> LastForegroundAt = new();

    /// <summary>Package name -> UTC time it was detected closed (timer frozen).</summary>
    public static readonly Dictionary<string, DateTime> StopTimes = new();

    /// <summary>Packages we have seen reach the foreground at least once.</summary>
    public static readonly HashSet<string> ForegroundSeen = new();

    /// <summary>The tracked package currently on screen, or null.</summary>
    public static string? CurrentForeground { get; set; }

    /// <summary>The package most recently launched.</summary>
    public static string? CurrentPackage { get; set; }

    /// <summary>Raised whenever the state above changes.</summary>
    public static event Action? Changed;

    public static void RaiseChanged() => Changed?.Invoke();

    public static bool IsStopped(string package) => StopTimes.ContainsKey(package);

    /// <summary>Clear all recorded timing (does not touch IsRunning).</summary>
    public static void Reset()
    {
        LaunchTimes.Clear();
        LastForegroundAt.Clear();
        StopTimes.Clear();
        ForegroundSeen.Clear();
        CurrentForeground = null;
        CurrentPackage = null;
        RaiseChanged();
    }
}
