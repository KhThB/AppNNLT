using Microsoft.Maui.Controls;

namespace Mobile.Controls;

public partial class EateryPinView : ContentView
{
    public EateryPinView()
    {
        InitializeComponent();
    }

    public EateryPinView(Mobile.Models.PoiModel poi, bool isHot = false)
    {
        InitializeComponent();
        
        // Binding Context cho toàn bộ View
        BindingContext = poi;

        if (!string.IsNullOrWhiteSpace(poi.ImageUrl))
        {
            if (poi.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                pinImage.Source = ImageSource.FromUri(new Uri(poi.ImageUrl));
            }
            else
            {
                pinImage.Source = ImageSource.FromFile(poi.ImageUrl);
            }
        }

        if (isHot)
        {
            ApplyHotStyle();
        }
    }

    private void ApplyHotStyle()
    {
        HotBadge.IsVisible = true;
        PinBorder.Stroke = Colors.Red;
        
        if (PinShadow != null)
        {
            PinShadow.Brush = Colors.Red;
            PinShadow.Radius = 20;
        }

        // Bắt đầu hiệu ứng nhấp nháy cho ngọn lửa
        StartPulseAnimation();
    }

    private async void StartPulseAnimation()
    {
        while (HotBadge != null && HotBadge.IsVisible)
        {
            await HotBadge.ScaleTo(1.3, 600, Easing.SinInOut);
            await HotBadge.ScaleTo(1.0, 600, Easing.SinInOut);
        }
    }
}
