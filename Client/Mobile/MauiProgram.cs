using Microsoft.Extensions.Logging;
using Mobile.Services;
using Mobile.ViewModels;
using Mobile.Views;
using SkiaSharp.Views.Maui.Controls.Hosting; 
using ZXing.Net.Maui.Controls;

namespace Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseBarcodeReader()
                .UseMauiApp<App>()
                // 1. FIX LỖI VĂNG APP: Bật động cơ vẽ bản đồ cho Mapsui
                .UseSkiaSharp()
                // XÓA BỎ dòng .UseMauiMaps() cũ đi
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialSymbolsOutlined.ttf", "MaterialIcons");
                });

            // 2. Đăng ký Service gọi API
            builder.Services.AddSingleton<OfflineStore>();
            builder.Services.AddSingleton<PoiService>();
            builder.Services.AddSingleton<NarrationPlaybackService>();
            builder.Services.AddSingleton<GeofenceMonitorService>();
            builder.Services.AddSingleton<ITrackingService, TrackingService>();

            // 3. Đăng ký các Trang (Views) và Bộ não (ViewModels)
            // Trang chủ
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<HomePageViewModel>();

            // Trang Chi tiết
            builder.Services.AddTransient<ViewModels.PoiDetailViewModel>();
            builder.Services.AddTransient<Views.PoiDetailPage>();
            // Trang Bản đồ
            builder.Services.AddTransient<MapPage>();
            builder.Services.AddTransient<MapPageViewModel>();

            // FIX NGUY CƠ TIỀM ẨN: Đăng ký 2 trang còn lại trong AppShell
            // Nếu bạn chưa tạo ViewModel cho 2 trang này thì cứ để đăng ký View thôi
            builder.Services.AddTransient<QrScannerPage>();
            builder.Services.AddTransient<ProfilePage>();

#if DEBUG   
            builder.Logging.AddDebug();
#endif

            // 4. Xóa đường viền gạch chân mặc định của Entry trên Android
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
            });

            return builder.Build();
        }
    }
}
