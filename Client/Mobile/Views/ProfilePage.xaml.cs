using Mobile.ViewModels;

namespace Mobile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = new ProfileViewModel();
    }
}