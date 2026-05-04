using Mobile.Views;

namespace Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    
        Routing.RegisterRoute(nameof(Views.PoiDetailPage), typeof(Views.PoiDetailPage));
    }
}