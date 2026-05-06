using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mobile.Models;
using Mobile.Services;
using System.Text.Json;

namespace Mobile.ViewModels;

[QueryProperty(nameof(Poi), "Poi")]
[QueryProperty(nameof(PoiId), "poiId")]
public partial class PoiDetailViewModel : ObservableObject
{
    private readonly PoiService _poiService;
    private readonly NarrationPlaybackService _playbackService;
    private CancellationTokenSource? _cts;
    private string? _poiId;

    [ObservableProperty]
    public partial PoiModel? Poi { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "Tiếng Việt";

    [ObservableProperty]
    public partial bool IsSpeaking { get; set; }

    [ObservableProperty]
    public partial string NarrationStatus { get; set; } = string.Empty;

    public string? PoiId
    {
        get => _poiId;
        set
        {
            if (SetProperty(ref _poiId, value) && !string.IsNullOrWhiteSpace(value))
            {
                _ = LoadPoiByIdAsync(value);
            }
        }
    }

    public PoiDetailViewModel(PoiService poiService, NarrationPlaybackService playbackService)
    {
        _poiService = poiService;
        _playbackService = playbackService;
    }

    public List<string> AvailableLanguages { get; } = new()
    {
        "Tiếng Việt", "English", "한국어", "日本語", "中文"
    };

    public string DisplayDescription => SelectedLanguage switch
    {
        "English" => Poi?.Description_EN ?? Poi?.SourceDescription ?? string.Empty,
        "한국어" => Poi?.Description_KO ?? Poi?.SourceDescription ?? string.Empty,
        "日本語" => Poi?.Description_JA ?? Poi?.SourceDescription ?? string.Empty,
        "中文" => Poi?.Description_ZH ?? Poi?.SourceDescription ?? string.Empty,
        _ => Poi?.Description_VI ?? Poi?.SourceDescription ?? string.Empty,
    };

    partial void OnPoiChanged(PoiModel? value)
    {
        if (value == null)
        {
            return;
        }

        var savedJson = Preferences.Default.Get("MyFavorites", "[]");
        var favList = JsonSerializer.Deserialize<List<PoiModel>>(savedJson) ?? new List<PoiModel>();
        IsFavorite = favList.Any(p => p.Id == value.Id);

        OnPropertyChanged(nameof(DisplayDescription));

        if (value.Distance == null || value.Distance == 0)
        {
            _ = CalculateDistanceLocallyAsync();
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayDescription));

        if (IsSpeaking)
        {
            _ = _playbackService.StopAsync();
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        if (Poi == null)
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không tìm thấy dữ liệu quán ăn.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(Poi.Id))
        {
            await Shell.Current.DisplayAlert("Lỗi", "POI chưa có mã định danh hợp lệ.", "OK");
            return;
        }

        var savedJson = Preferences.Default.Get("MyFavorites", "[]");
        var favList = JsonSerializer.Deserialize<List<PoiModel>>(savedJson) ?? new List<PoiModel>();
        var existingPoi = favList.FirstOrDefault(p => p.Id == Poi.Id);

        if (existingPoi != null)
        {
            favList.Remove(existingPoi);
            IsFavorite = false;
            await Shell.Current.DisplayAlert("Đã bỏ lưu", $"Đã xóa {Poi.Name}", "OK");
        }
        else
        {
            favList.Add(Poi);
            IsFavorite = true;
            await Shell.Current.DisplayAlert("Thành công", $"Đã lưu {Poi.Name}", "OK");
        }

        Preferences.Default.Set("MyFavorites", JsonSerializer.Serialize(favList));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task SpeakDescriptionAsync()
    {
        if (IsSpeaking)
        {
            await _playbackService.StopAsync();
            NarrationStatus = "Đang dừng thuyết minh...";
            return;
        }

        if (Poi == null || string.IsNullOrWhiteSpace(Poi.Id))
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không tìm thấy POI để phát thuyết minh.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayDescription))
        {
            await Shell.Current.DisplayAlert("Thông báo", "Chưa có bài thuyết minh cho ngôn ngữ này.", "OK");
            return;
        }

        _cts = new CancellationTokenSource();

        try
        {
            IsSpeaking = true;
            NarrationStatus = "Đang phát thuyết minh...";

            var result = await _playbackService.PlayAsync(Poi, SelectedLanguageCode, DisplayDescription, _cts.Token);
            if (!result.Success)
            {
                NarrationStatus = result.ErrorMessage;
                if (!string.Equals(result.ErrorMessage, "Đã dừng thuyết minh.", StringComparison.OrdinalIgnoreCase))
                {
                    await Shell.Current.DisplayAlert("Lỗi", result.ErrorMessage, "OK");
                }
                return;
            }

            NarrationStatus = result.RateLimited
                ? "Bạn đang nghe lại trong giới hạn chống spam; lượt này không cộng số."
                : result.UsedAudioFile ? "Đã phát audio thuyết minh." : "Đã phát TTS thuyết minh.";
        }
        finally
        {
            IsSpeaking = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    public async Task OpenMapAsync()
    {
        if (Poi == null || (Poi.Latitude == 0 && Poi.Longitude == 0))
        {
            return;
        }

        await Map.Default.OpenAsync(Poi.Latitude, Poi.Longitude, new MapLaunchOptions
        {
            Name = Poi.Name ?? "POI",
            NavigationMode = NavigationMode.Driving,
        });
    }

    private async Task CalculateDistanceLocallyAsync()
    {
        if (Poi == null || (Poi.Latitude == 0 && Poi.Longitude == 0))
        {
            return;
        }

        try
        {
            var userLocation = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            if (userLocation == null)
            {
                return;
            }

            var distanceKm = Location.CalculateDistance(
                userLocation.Latitude,
                userLocation.Longitude,
                Poi.Latitude,
                Poi.Longitude,
                DistanceUnits.Kilometers);

            Poi.Distance = distanceKm * 1000;
            OnPropertyChanged(nameof(Poi));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to calculate local distance: {ex.Message}");
        }
    }

    private string SelectedLanguageCode => AvailableLanguages.IndexOf(SelectedLanguage) switch
    {
        1 => "EN",
        2 => "KO",
        3 => "JA",
        4 => "ZH",
        _ => "VI",
    };

    private async Task LoadPoiByIdAsync(string poiId)
    {
        try
        {
            var poi = await _poiService.GetPoiByIdAsync(poiId);
            if (poi != null)
            {
                Poi = poi;
            }
            else
            {
                NarrationStatus = "Khong tim thay POI nay.";
            }
        }
        catch (Exception ex)
        {
            NarrationStatus = "Khong mo duoc POI tu thong bao.";
            System.Diagnostics.Debug.WriteLine($"Failed to load POI from notification: {ex.Message}");
        }
    }
}
