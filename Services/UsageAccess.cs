using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.OS;
using Android.Provider;
using Application = Android.App.Application;

namespace AppOpenerAndTimer.Services;

/// <summary>
/// Wraps the "Usage access" special permission and the UsageStatsManager query
/// we use to tell which app is currently in the foreground (so we can tell when
/// a launched app has been closed / switched away from).
/// </summary>
public static class UsageAccess
{
    const string GetUsageStatsOp = "android:get_usage_stats";

    /// <summary>True if the user has granted this app "Usage access".</summary>
    public static bool IsGranted()
    {
        var ctx = Application.Context;
        var appOps = (AppOpsManager)ctx.GetSystemService(Context.AppOpsService)!;

        AppOpsManagerMode mode = Build.VERSION.SdkInt >= BuildVersionCodes.Q
            ? appOps.UnsafeCheckOpNoThrow(GetUsageStatsOp, Process.MyUid(), ctx.PackageName!)
#pragma warning disable CA1422
            : appOps.CheckOpNoThrow(GetUsageStatsOp, Process.MyUid(), ctx.PackageName!);
#pragma warning restore CA1422

        return mode == AppOpsManagerMode.Allowed;
    }

    /// <summary>Opens the system "Usage access" settings screen.</summary>
    public static void OpenSettings()
    {
        var intent = new Intent(Settings.ActionUsageAccessSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        Application.Context.StartActivity(intent);
    }

    /// <summary>
    /// Returns the package that most recently came to the foreground within the
    /// last <paramref name="windowSeconds"/> seconds, or null if none was seen.
    /// </summary>
    public static string? LatestForeground(int windowSeconds = 15)
    {
        var ctx = Application.Context;
        var usm = (UsageStatsManager)ctx.GetSystemService(Context.UsageStatsService)!;

        long end = Java.Lang.JavaSystem.CurrentTimeMillis();
        long begin = end - windowSeconds * 1000L;

        var events = usm.QueryEvents(begin, end);
        var e = new UsageEvents.Event();
        string? foreground = null;

        while (events!.HasNextEvent)
        {
            events.GetNextEvent(e);
            if (e.EventType == UsageEventType.MoveToForeground)
                foreground = e.PackageName;
        }

        return foreground;
    }
}
