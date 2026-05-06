using Android.App;
using Android.Runtime;

namespace Mobile
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp()
        {
            var app = MauiProgram.CreateMauiApp();
            ServiceProvider = app.Services;
            return app;
        }
    }
}
