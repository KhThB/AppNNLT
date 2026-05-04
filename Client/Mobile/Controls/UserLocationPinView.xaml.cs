using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Mobile.Controls;

public partial class UserLocationPinView : ContentView
{
    private bool _isAnimating;

    public UserLocationPinView()
    {
        InitializeComponent();
        
        _isAnimating = true;
        StartPulseAnimation();
    }

    private async void StartPulseAnimation()
    {
        // Vòng lặp vô tận tạo hiệu ứng nhịp đập (pulse)
        while (_isAnimating)
        {
            try
            {
                // Trả về trạng thái ban đầu
                pulseRing.Scale = 1.0;
                pulseRing.Opacity = 1.0;

                // Chạy đồng thời 2 hiệu ứng Scale (phình ra) và Fade (mờ dần về 0) trong 1 giây (1000ms)
                var scaleTask = pulseRing.ScaleTo(1.5, 1000, Easing.CubicOut);
                var fadeTask = pulseRing.FadeTo(0, 1000, Easing.CubicOut);

                // Đợi cả 2 hiệu ứng hoàn tất
                await Task.WhenAll(scaleTask, fadeTask);

                // Delay một chút trước khi nhịp đập tiếp theo bắt đầu
                await Task.Delay(200);
            }
            catch
            {
                // Bắt lỗi nếu view bị hủy giữa chừng
                break;
            }
        }
    }
}
