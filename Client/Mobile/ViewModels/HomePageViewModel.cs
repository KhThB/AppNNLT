using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mobile.Models;
using Mobile.Services;
using Mobile.Views;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Mobile.ViewModels;

public partial class HomePageViewModel : ObservableObject
{
    private readonly PoiService _poiService;
    private readonly List<PoiModel> _allPois = new();
    private Location? _currentLocation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial string SelectedCategory { get; set; } = "Tất cả";

    public HomePageViewModel(PoiService poiService)
    {
        _poiService = poiService;
        Title = "Vinh Khanh Food Tour";
    }

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<PoiModel> Pois { get; } = new();
    public ObservableCollection<PoiModel> NearbyPois { get; } = new();

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Tất cả", "Ốc Đêm", "Ăn Vặt", "Trà Sữa", "Nhậu", "Cơm & Bún",
    };

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilters();
    }

    [RelayCommand]
    public async Task GetPoisAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            _allPois.Clear();

            var items = await _poiService.GetPoisAsync();
            foreach (var item in items)
            {
                _allPois.Add(item);
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Lỗi kết nối", $"Không thể lấy dữ liệu: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadNearbyPoisAsync()
    {
        try
        {
            NearbyPois.Clear();

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            Location? location = null;
            if (status == PermissionStatus.Granted)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                location = await Geolocation.Default.GetLocationAsync(request);
                _currentLocation = location;
                
                // Update distances for all existing POIs
                if (_currentLocation != null)
                {
                    foreach (var p in _allPois)
                    {
                        var d = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(_currentLocation.Latitude, _currentLocation.Longitude, p.Latitude, p.Longitude, DistanceUnits.Kilometers);
                        p.Distance = d * 1000;
                    }
                    // Refresh the lists to trigger UI updates
                    ApplyFilters();
                }
            }

            if (location == null)
            {
                AddFallbackNearby(null);
                return;
            }

            var items = await _poiService.GetNearbyPoisAsync(location.Longitude, location.Latitude, 5000);
            foreach (var item in items)
            {
                var dist = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(location.Latitude, location.Longitude, item.Latitude, item.Longitude, DistanceUnits.Kilometers);
                item.Distance = dist * 1000;
                NearbyPois.Add(item);
            }

            if (NearbyPois.Count == 0)
            {
                AddFallbackNearby(location);
            }
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            AddFallbackNearby(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load nearby POIs: {ex.Message}");
            AddFallbackNearby(null);
        }
    }

    [RelayCommand]
    private void Search(string? query)
    {
        SearchText = query;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task GoToDetails(PoiModel poi)
    {
        if (poi == null)
        {
            return;
        }

        await Shell.Current.GoToAsync(nameof(PoiDetailPage), true, new Dictionary<string, object>
        {
            { "Poi", poi },
        });
    }

    private void ApplyFilters()
    {
        var query = Normalize(SearchText ?? string.Empty);
        var category = Normalize(SelectedCategory);

        var filtered = _allPois.Where(poi =>
        {
            var matchesQuery = string.IsNullOrWhiteSpace(query)
                || Normalize(poi.Name ?? string.Empty).Contains(query)
                || Normalize(poi.Address ?? string.Empty).Contains(query)
                || poi.Tags.Any(tag => Normalize(tag).Contains(query));

            var matchesCategory = category == "tat ca"
                || poi.Tags.Any(tag => Normalize(tag) == category)
                || Normalize(poi.TagSummary).Contains(category);

            return matchesQuery && matchesCategory;
        }).ToList();

        Pois.Clear();
        foreach (var item in filtered)
        {
            Pois.Add(item);
        }
    }

    private void AddFallbackNearby(Location? userLocation)
    {
        NearbyPois.Clear();
        foreach (var item in _allPois.Take(10))
        {
            if (userLocation != null)
            {
                var dist = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(userLocation.Latitude, userLocation.Longitude, item.Latitude, item.Longitude, DistanceUnits.Kilometers);
                item.Distance = dist * 1000;
            }
            else
            {
                item.Distance = 0; // or null if we don't want to show it
            }
            NearbyPois.Add(item);
        }
    }

    private static string Normalize(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c == 'đ')
            {
                builder.Append('d');
                continue;
            }

            if (c == 'Đ')
            {
                builder.Append('D');
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var compact = builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("&", string.Empty)
            .Replace("-", " ")
            .Replace("_", " ");

        return string.Join(" ", compact.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
