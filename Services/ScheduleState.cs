namespace AppOpenerAndTimer.Services;

/// <summary>
/// Process-wide state shared between the foreground service (which owns the
/// launch schedule and timing) and the UI (which reads it to render timers).
/// </summary>
public static class ScheduleState
{
    /// <summary>True while the scheduler service is still launching apps.</summary>
    public static bool IsRunning { get; set; }

    /// <summary>Package name -> UTC time it was launched.</summary>
    public static readonly Dictionary<string, DateTime> LaunchTimes = new();

    /// <summary>Package name -> UTC time we detected it was closed. Frozen once set.</summary>
    public static readonly Dictionary<string, DateTime> ClosedTimes = new();

    /// <summary>Packages we have seen reach the foreground at least once.</summary>
    public static readonly HashSet<string> ForegroundSeen = new();

    /// <summary>The package most recently launched.</summary>
    public static string? CurrentPackage { get; set; }

    /// <summary>Raised whenever the state above changes.</summary>
    public static event Action? Changed;

    public static void RaiseChanged() => Changed?.Invoke();

    /// <summary>Clear all recorded timing (does not touch IsRunning).</summary>
    public static void Reset()
    {
        LaunchTimes.Clear();
        ClosedTimes.Clear();
        ForegroundSeen.Clear();
        CurrentPackage = null;
        RaiseChanged();
    }
}
