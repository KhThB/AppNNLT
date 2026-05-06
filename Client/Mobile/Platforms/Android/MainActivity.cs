using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Mobile
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            StorePendingPoiNavigation(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            if (intent != null)
            {
                Intent = intent;
            }

            StorePendingPoiNavigation(intent);
            MainThread.BeginInvokeOnMainThread(() => _ = App.TryNavigateToPendingPoiAsync());
        }

        private static void StorePendingPoiNavigation(Intent? intent)
        {
            var poiId = intent?.GetStringExtra(App.PendingPoiNavigationKey);
            if (!string.IsNullOrWhiteSpace(poiId))
            {
                Preferences.Default.Set(App.PendingPoiNavigationKey, poiId);
            }
        }
    }
}
