using Mobile.ViewModels;

namespace Mobile.Views;

public partial class PoiDetailPage : ContentPage
{
    // BẮT BUỘC PHẢI TRUYỀN PoiDetailViewModel VÀO ĐÂY
    public PoiDetailPage(PoiDetailViewModel viewModel)
    {
        InitializeComponent();

        // DÒNG NÀY LÀ SỢI DÂY ĐIỆN NỐI GIAO DIỆN VÀ CODE
        BindingContext = viewModel;
    }

    private async void OnBackClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ContentScroll_Scrolled(object? sender, ScrolledEventArgs e)
    {
        // Khi người dùng cuộn xuống (tức là tọa độ Y tăng lên)
        if (e.ScrollY > 0)
        {
            // Di chuyển ảnh lên trên với tốc độ bằng một nửa tốc độ cuộn
            HeaderImage.TranslationY = -(e.ScrollY * 0.5);

            // Hiệu ứng mờ dần (Fade out): 
            // Giả sử chiều cao ảnh là 300, khi cuộn được 300px thì ảnh mờ hoàn toàn
            double opacity = 1 - (e.ScrollY / 300);
            HeaderImage.Opacity = opacity < 0 ? 0 : opacity;
        }
        else
        {
            // Trả về trạng thái gốc khi cuộn kịch trần (hoặc pull-to-refresh)
            HeaderImage.TranslationY = 0;
            HeaderImage.Opacity = 1;
        }
    }
}