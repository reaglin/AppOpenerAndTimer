using Android.App;
using Android.Content.PM;
using Android.OS;

namespace AppOpenerAndTimer;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
        {
            RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, 100);
        }
    }
}
