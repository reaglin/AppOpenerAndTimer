using Android.Content;
using Android.Content.PM;

namespace AppOpenerAndTimer.Services;

/// <summary>
/// Android helpers to enumerate launchable apps and start them.
/// </summary>
public static class AppLauncher
{
    /// <summary>Returns every app that has a launcher entry, minus this app itself.</summary>
    public static List<(string Package, string Label)> GetLaunchableApps()
    {
        var ctx = Android.App.Application.Context;
        var pm = ctx.PackageManager!;
        var self = ctx.PackageName;

        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);

        var results = new List<(string, string)>();
        var seen = new HashSet<string>();

#pragma warning disable CA1422 // Old flags overload works across all supported API levels
        var infos = pm.QueryIntentActivities(intent, (PackageInfoFlags)0);
#pragma warning restore CA1422

        foreach (var ri in infos)
        {
            var pkg = ri.ActivityInfo?.PackageName;
            if (string.IsNullOrEmpty(pkg) || pkg == self || !seen.Add(pkg))
                continue;

            var label = ri.LoadLabel(pm)?.ToString();
            if (string.IsNullOrWhiteSpace(label))
                label = pkg;

            results.Add((pkg, label));
        }

        results.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    /// <summary>Launches an app by package name. Returns false if it has no launch intent.</summary>
    public static bool Launch(string package)
    {
        var ctx = Android.App.Application.Context;
        var intent = ctx.PackageManager!.GetLaunchIntentForPackage(package);
        if (intent is null)
            return false;

        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
        return true;
    }

    /// <summary>True if the "Display over other apps" permission is granted.</summary>
    public static bool CanDrawOverlays()
        => Android.Provider.Settings.CanDrawOverlays(Android.App.Application.Context);
}
