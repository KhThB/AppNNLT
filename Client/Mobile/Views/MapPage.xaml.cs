using Mapsui.Extensions;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mobile.Models;
using Mobile.ViewModels;
using Mapsui;

namespace Mobile.Views;

public partial class MapPage : ContentPage
{
    private MapPageViewModel _viewModel;
    private bool _isInitializationDone = false;

    public MapPage(MapPageViewModel viewModel)
    {
        // 1. LUÔN LUÔN gọi InitializeComponent đầu tiên
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        if (mapView.Map != null)
        {
            // 🚨 TẮT CHẤM XANH NGOÀI BIỂN: Phải đặt sau InitializeComponent
            mapView.MyLocationLayer.Enabled = false;

            // Nạp lớp bản đồ nền
            UpdateMapTheme();

            // Ẩn các thông số Performance/Logging
            foreach (var widget in mapView.Map.Widgets.ToList())
            {
                if (widget.GetType().Name.Contains("Logging") || widget.GetType().Name.Contains("Performance"))
                    widget.Enabled = false;
            }
        }

        mapView.PinClicked += OnMapPinClicked;

        // Lắng nghe sự kiện thay đổi Theme
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateMapTheme();
                    mapView.Refresh();
                });
            };
        }
    }

    private void UpdateMapTheme()
    {
        if (mapView.Map == null) return;

        // Xóa các lớp bản đồ nền cũ nếu có
        var baseLayers = mapView.Map.Layers.Where(l => l.Name == "OpenStreetMap" || l.Name == "CartoDB Dark Matter").ToList();
        foreach (var layer in baseLayers)
        {
            mapView.Map.Layers.Remove(layer);
        }

        bool isDarkMode = Application.Current?.UserAppTheme == AppTheme.Dark;

        if (isDarkMode)
        {
            mapView.Map.Layers.Insert(0, CreateDarkTileLayer());
        }
        else
        {
            mapView.Map.Layers.Insert(0, OpenStreetMap.CreateTileLayer());
        }
    }

    private Mapsui.Layers.ILayer CreateDarkTileLayer()
    {
        var tileSource = new BruTile.Web.HttpTileSource(
            new BruTile.Predefined.GlobalSphericalMercator(),
            "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png",
            new[] { "a", "b", "c", "d" },
            name: "CartoDB Dark Matter");

        return new Mapsui.Tiling.Layers.TileLayer(tileSource) { Name = "CartoDB Dark Matter" };
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (_isInitializationDone)
        {
            mapView.Refresh();
            return;
        }

        // Zoom về trung tâm Phố Vĩnh Khánh
        if (mapView.Map != null)
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(106.7044, 10.7626);
            mapView.Map.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 2.0);
        }

        await _viewModel.LoadPinsAsync();

        // Vẽ ghim trên luồng chính để không bị lớp bản đồ đè lên
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            LoadMapPins();
            await ShowUserLocation();
            mapView.Refresh();
        });

        _isInitializationDone = true;
    }

    private Mapsui.MPoint _userGeoLocation = new(0, 0);
    private Mobile.Controls.UserLocationPinView? _userPinView;
    private Dictionary<Mobile.Controls.EateryPinView, Mapsui.MPoint> _eateryPins = new();

    private void LoadMapPins()
    {
        markersLayer.Children.Clear();
        _eateryPins.Clear();

        // 1. Tạo Pin cho người dùng (Vị trí mặc định ở Vĩnh Khánh nếu chưa lấy được GPS)
        _userPinView = new Mobile.Controls.UserLocationPinView();
        _userPinView.HorizontalOptions = LayoutOptions.Start;
        _userPinView.VerticalOptions = LayoutOptions.Start;
        
        var (ux, uy) = Mapsui.Projections.SphericalMercator.FromLonLat(106.7044, 10.7626);
        _userGeoLocation = new Mapsui.MPoint(ux, uy);
        markersLayer.Children.Add(_userPinView);

        // 2. Render toàn bộ quán ăn từ dữ liệu thật (_viewModel.Pois)
        if (_viewModel.Pois != null)
        {
            foreach (var poi in _viewModel.Pois)
            {
                if (poi.Latitude == 0 && poi.Longitude == 0) continue;

                bool isHot = (poi.CountedQrScanCount ?? 0) >= 10;
                var pinView = new Mobile.Controls.EateryPinView(poi, isHot)
                {
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start
                };

                // Thêm sự kiện Click cho Pin để điều hướng đến trang chi tiết
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) => 
                {
                    // Hiệu ứng nhún nhẹ khi click vào ghim
                    await pinView.ScaleTo(0.9, 100);
                    pinView.Scale = 1.0;

                    await Shell.Current.GoToAsync(nameof(PoiDetailPage), true, new Dictionary<string, object>
                    {
                        { "Poi", poi }
                    });
                };
                pinView.GestureRecognizers.Add(tapGesture);

                // Chuyển đổi tọa độ thật sang tọa độ bản đồ Mapsui
                var (ex, ey) = Mapsui.Projections.SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                var geoLoc = new Mapsui.MPoint(ex, ey);

                _eateryPins.Add(pinView, geoLoc);
                markersLayer.Children.Add(pinView);

                // Sau khi render xong kích thước, định vị lại vị trí trên màn hình
                pinView.SizeChanged += (s, e) => UpdateMarkersPosition();
            }
        }

        // Đăng ký event thay đổi góc nhìn (chỉ đăng ký 1 lần)
        if (mapView.Map != null)
        {
            mapView.Map.Navigator.ViewportChanged -= OnViewportChanged;
            mapView.Map.Navigator.ViewportChanged += OnViewportChanged;
        }

        UpdateMarkersPosition();
    }

    private void OnViewportChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdateMarkersPosition());
    }

    private void UpdateMarkersPosition()
    {
        if (mapView.Map == null) return;

        // Cập nhật vị trí ghim người dùng
        if (_userPinView != null)
        {
            var screenPos = mapView.Map.Navigator.Viewport.WorldToScreen(_userGeoLocation);
            // Anchor = (0.5, 0.5) cho Pin tròn của người dùng
            _userPinView.TranslationX = screenPos.X - (_userPinView.Width / 2);
            _userPinView.TranslationY = screenPos.Y - (_userPinView.Height / 2);
        }

        // Cập nhật vị trí hàng loạt ghim quán ăn
        foreach (var kvp in _eateryPins)
        {
            var view = kvp.Key;
            var geoLoc = kvp.Value;

            var screenPos = mapView.Map.Navigator.Viewport.WorldToScreen(geoLoc);
            
            // --- GIẢI THÍCH ANCHOR (0.5, 1.0) ---
            // 0.5 (X): Trừ đi một nửa chiều rộng (w / 2) để căn giữa mũi nhọn theo chiều ngang.
            // 1.0 (Y): Trừ đi toàn bộ chiều cao (h) để kéo nguyên cái CustomView lên trên, 
            //          khiến điểm dưới cùng (đáy mũi nhọn) khớp chính xác vào tọa độ GPS.
            double w = view.Width > 0 ? view.Width : 150;
            double h = view.Height > 0 ? view.Height : 50;
            
            view.TranslationX = screenPos.X - (w / 2);
            view.TranslationY = screenPos.Y - h;

            // Ẩn ghim nếu nằm ngoài màn hình để tối ưu hóa hiệu năng
            bool isVisible = screenPos.X >= -w && screenPos.X <= mapView.Width + w &&
                             screenPos.Y >= -h && screenPos.Y <= mapView.Height + h;
            view.IsVisible = isVisible;
        }
    }

    private async Task ShowUserLocation()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    // Cập nhật vị trí thật cho User Location Pin
                    var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                    _userGeoLocation = new Mapsui.MPoint(x, y);
                    
                    MainThread.BeginInvokeOnMainThread(() => 
                    {
                        UpdateMarkersPosition();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi lấy vị trí: {ex.Message}");
        }
    }

    private async void OnMapPinClicked(object? sender, PinClickedEventArgs e)
    {
        if (e.Pin == null) return;
        e.Handled = true;

        if (e.Pin.Tag is PoiModel clickedPoi)
        {
            await Shell.Current.GoToAsync(nameof(PoiDetailPage), true, new Dictionary<string, object>
            {
                { "Poi", clickedPoi }
            });
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        mapView.Map?.Navigator.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        mapView.Map?.Navigator.ZoomOut();
    }

    private void OnCenterMapClicked(object? sender, EventArgs e)
    {
        CenterToVinhKhanh();
    }

    private void CenterToVinhKhanh()
    {
        if (mapView.Map != null)
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(106.7044, 10.7626);
            mapView.Map.Navigator.FlyTo(new Mapsui.MPoint(x, y), 2.0, 1000);
        }
    }


}
