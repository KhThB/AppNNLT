using Mobile.ViewModels;

namespace Mobile.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomePageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel; // Kết nối giao diện với bộ não ViewModel
    }

   protected override async void OnAppearing()
{
    base.OnAppearing();
    
    // Ép kiểu BindingContext về ViewModel để gọi Command
    if (BindingContext is HomePageViewModel viewModel)
    {
        // Tự động tải dữ liệu khi mở App
        await viewModel.GetPoisCommand.ExecuteAsync(null);
        // Tên command trong ViewModel là LoadNearbyPoisCommand
        await viewModel.LoadNearbyPoisCommand.ExecuteAsync(null);
    }
}
}