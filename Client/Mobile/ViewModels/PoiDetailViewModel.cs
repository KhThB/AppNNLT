using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mobile.Models;
using Mobile.Services;
using System.Text.Json;

namespace Mobile.ViewModels;

[QueryProperty(nameof(Poi), "Poi")]
public partial class PoiDetailViewModel : ObservableObject
{
    private readonly PoiService _poiService;
    private CancellationTokenSource? _cts;
    private string? _currentNarrationLogId;
    private DateTime? _narrationStartedAt;

    [ObservableProperty]
    public partial PoiModel? Poi { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "Tiếng Việt";

    [ObservableProperty]
    public partial bool IsSpeaking { get; set; }

    public PoiDetailViewModel(PoiService poiService)
    {
        _poiService = poiService;
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
            _cts?.Cancel();
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

    [RelayCommand]
    public async Task SpeakDescriptionAsync()
    {
        if (IsSpeaking)
        {
            _cts?.Cancel();
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

        var play = await _poiService.StartNarrationAsync(Poi.Id, SelectedLanguageCode);
        if (play == null)
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không ghi nhận được lượt nghe thuyết minh.", "OK");
            return;
        }

        _currentNarrationLogId = play.LogId;
        _narrationStartedAt = DateTime.UtcNow;
        _cts = new CancellationTokenSource();

        try
        {
            IsSpeaking = true;

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            Locale? locale = SelectedLanguage switch
            {
                "English" => locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                "한국어" => locales.FirstOrDefault(l => l.Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase)),
                "日本語" => locales.FirstOrDefault(l => l.Language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)),
                "中文" => locales.FirstOrDefault(l => l.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)),
                _ => locales.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase)),
            };

            var options = locale == null ? new SpeechOptions() : new SpeechOptions { Locale = locale };
            await TextToSpeech.Default.SpeakAsync(DisplayDescription, options, cancelToken: _cts.Token);
            await FinishCurrentNarrationAsync("Completed");
        }
        catch (OperationCanceledException)
        {
            await FinishCurrentNarrationAsync("Stopped");
        }
        catch (Exception)
        {
            await FinishCurrentNarrationAsync("Error", "PLAYBACK_FAILED");
            await Shell.Current.DisplayAlert("Lỗi", "Không thể phát giọng đọc trên thiết bị này.", "OK");
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

    private async Task FinishCurrentNarrationAsync(string status, string errorCode = "")
    {
        if (string.IsNullOrWhiteSpace(_currentNarrationLogId))
        {
            return;
        }

        var dwell = _narrationStartedAt.HasValue
            ? Math.Max(0, (int)Math.Round((DateTime.UtcNow - _narrationStartedAt.Value).TotalSeconds))
            : 0;

        await _poiService.FinishNarrationAsync(_currentNarrationLogId, status, dwell, errorCode);
        _currentNarrationLogId = null;
        _narrationStartedAt = null;
    }

    private string SelectedLanguageCode => AvailableLanguages.IndexOf(SelectedLanguage) switch
    {
        1 => "EN",
        2 => "KO",
        3 => "JA",
        4 => "ZH",
        _ => "VI",
    };
}
