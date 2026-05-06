using Microsoft.Maui.Controls;

namespace Mobile.Controls;

public partial class BottomTabBar : ContentView
{
    public BottomTabBar()
    {
        InitializeComponent();
    }

    private async void OnHomeClicked(object? sender, TappedEventArgs e)
    {
        await AnimateIconAndNavigate((View?)sender, "//HomePage");
    }

    private async void OnMapClicked(object? sender, TappedEventArgs e)
    {
        await AnimateIconAndNavigate((View?)sender, "//MapPage");
    }

    private async void OnQrClicked(object? sender, TappedEventArgs e)
    {
        var view = (View?)sender;
        if (view != null)
        {
            // Hiệu ứng thu nhỏ nhanh
            await view.ScaleTo(0.8, 100, Easing.CubicOut);
            // Phình to ra (Bounce effect) tạo cảm giác như nút vật lý
            _ = view.ScaleTo(1.0, 150, Easing.SpringOut);
        }

        var currentPage = Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            await currentPage.FadeTo(0.3, 100, Easing.CubicOut);
        }

        await Shell.Current.GoToAsync("//QrScannerPage");

        if (currentPage != null)
        {
            currentPage.Opacity = 1;
        }
    }



    private async void OnProfileClicked(object? sender, TappedEventArgs e)
    {
        await AnimateIconAndNavigate((View?)sender, "//ProfilePage");
    }

    private async Task AnimateIconAndNavigate(View? view, string route)
    {
        // 1. Animate the tab item being clicked
        if (view != null)
        {
            await view.ScaleTo(0.85, 80, Easing.CubicOut);
            _ = view.ScaleTo(1.0, 150, Easing.SpringOut);
        }

        // 2. Animate page transition (Fade out current page)
        var currentPage = Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            await currentPage.FadeTo(0.3, 100, Easing.CubicOut);
        }

        // 3. Thực hiện chuyển Tab
        await Shell.Current.GoToAsync(route);

        // 4. Phục hồi trạng thái cho trang cũ
        if (currentPage != null)
        {
            currentPage.Opacity = 1;
        }
    }
}
