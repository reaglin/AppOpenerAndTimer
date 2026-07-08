namespace AppOpenerAndTimer.Services;

/// <summary>
/// Process-wide state shared between the foreground service (which owns the
/// launch schedule and timing) and the UI (which reads it to render timers).
///
/// Each app's timer is the total time it has actually been on screen
/// (accumulated foreground time), which is robust to the brief foreground
/// blips that happen during app-open animations.
/// </summary>
public static class ScheduleState
{
    /// <summary>True while the scheduler service is still launching apps.</summary>
    public static bool IsRunning { get; set; }

    /// <summary>Package name -> UTC time it was launched.</summary>
    public static readonly Dictionary<string, DateTime> LaunchTimes = new();

    /// <summary>Package name -> accumulated milliseconds the app has been on screen.</summary>
    public static readonly Dictionary<string, long> ForegroundMs = new();

    /// <summary>Packages we have seen reach the foreground at least once.</summary>
    public static readonly HashSet<string> ForegroundSeen = new();

    /// <summary>The tracked package currently on screen, or null.</summary>
    public static string? CurrentForeground { get; set; }

    /// <summary>The package most recently launched.</summary>
    public static string? CurrentPackage { get; set; }

    /// <summary>Raised whenever the state above changes.</summary>
    public static event Action? Changed;

    public static void RaiseChanged() => Changed?.Invoke();

    /// <summary>Milliseconds an app has been on screen so far.</summary>
    public static long ForegroundMillis(string package)
        => ForegroundMs.TryGetValue(package, out var ms) ? ms : 0;

    /// <summary>Clear all recorded timing (does not touch IsRunning).</summary>
    public static void Reset()
    {
        LaunchTimes.Clear();
        ForegroundMs.Clear();
        ForegroundSeen.Clear();
        CurrentForeground = null;
        CurrentPackage = null;
        RaiseChanged();
    }
}
