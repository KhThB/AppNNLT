using System.Net.Http.Json;
using System.Text.Json;
using Mobile.Models;

namespace Mobile.Services;

public sealed class PoiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _backendBaseUrls;

    public PoiService()
    {
        _backendBaseUrls = ResolveBackendBaseUrls();
        _httpClient = new HttpClient(CreateHttpMessageHandler())
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public string DeviceId => EnsurePreference("TourGuideDeviceId", "device");
    public string VisitorId => EnsurePreference("TourGuideVisitorId", "visitor");
    public string SessionId => EnsurePreference("TourGuideSessionId", "session");

    public async Task<List<PoiModel>> GetPoisAsync()
    {
        try
        {
            var page = await GetFromJsonWithFallbackAsync<PagedResponse<PoiModel>>(
                "api/poi/approved?page=1&pageSize=100",
                JsonOptions);

            return page?.Items?.Select(NormalizePoi).ToList() ?? new List<PoiModel>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load approved POIs: {ex.Message}");
            throw;
        }
    }

    public async Task<PoiModel?> GetPoiByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            using var response = await GetWithFallbackAsync($"api/poi/details/{Uri.EscapeDataString(id)}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var detail = await response.Content.ReadFromJsonAsync<PoiPublicDetailResponse>(JsonOptions);
            return detail == null ? null : MapPublicDetail(detail);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load POI detail: {ex.Message}");
            return null;
        }
    }

    public async Task<List<PoiModel>> GetNearbyPoisAsync(double longitude, double latitude, double maxDistanceInMeters)
    {
        try
        {
            var url = $"api/poi/nearby?longitude={longitude}&latitude={latitude}&maxDistance={maxDistanceInMeters}";
            var items = await GetFromJsonWithFallbackAsync<List<PoiModel>>(url, JsonOptions);
            return items?.Select(NormalizePoi).ToList() ?? new List<PoiModel>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load nearby POIs: {ex.Message}");
            return new List<PoiModel>();
        }
    }

    public async Task<QrScanResult?> RecordQrScanAsync(string poiId)
    {
        try
        {
            using var response = await PostAsJsonWithFallbackAsync(
                "api/qr/scan",
                new
                {
                    poiId,
                    visitorId = VisitorId,
                    sessionId = SessionId,
                    triggerSource = "MobileQR",
                });

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<QrScanResult>(JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to record QR scan: {ex.Message}");
            return null;
        }
    }

    public async Task<NarrationPlayResult?> StartNarrationAsync(string poiId, string language = "VI")
    {
        try
        {
            using var response = await PostAsJsonWithFallbackAsync(
                "api/narration/play",
                new
                {
                    poiId,
                    visitorId = VisitorId,
                    sessionId = SessionId,
                    language,
                    triggerSource = "MobileTTS",
                });

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<NarrationPlayResult>(JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start narration: {ex.Message}");
            return null;
        }
    }

    public async Task FinishNarrationAsync(string logId, string status, int dwellTimeSeconds, string errorCode = "")
    {
        if (string.IsNullOrWhiteSpace(logId))
        {
            return;
        }

        try
        {
            using var response = await PostAsJsonWithFallbackAsync(
                "api/narration/finish",
                new
                {
                    logId,
                    status,
                    dwellTimeSeconds,
                    errorCode,
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to finish narration: {ex.Message}");
        }
    }

    public async Task SendPingAsync()
    {
        double latitude = 0;
        double longitude = 0;
        double speed = 0;

        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));

            if (location != null)
            {
                latitude = location.Latitude;
                longitude = location.Longitude;
                speed = location.Speed ?? 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Location unavailable for ping: {ex.Message}");
        }

        try
        {
            using var response = await PostAsJsonWithFallbackAsync(
                "api/tracking/ping",
                new
                {
                    deviceId = DeviceId,
                    userId = "",
                    sessionId = SessionId,
                    latitude,
                    longitude,
                    speed,
                    platform = DeviceInfo.Platform.ToString(),
                    appVersion = AppInfo.VersionString,
                    deviceName = DeviceInfo.Name,
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send ping: {ex.Message}");
        }
    }

    private async Task<T?> GetFromJsonWithFallbackAsync<T>(string relativeUrl, JsonSerializerOptions options)
    {
        using var response = await GetWithFallbackAsync(relativeUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(options);
    }

    private Task<HttpResponseMessage> GetWithFallbackAsync(string relativeUrl)
    {
        return SendWithFallbackAsync(baseUrl => _httpClient.GetAsync(BuildUri(baseUrl, relativeUrl)));
    }

    private Task<HttpResponseMessage> PostAsJsonWithFallbackAsync<TValue>(string relativeUrl, TValue value)
    {
        return SendWithFallbackAsync(baseUrl => _httpClient.PostAsJsonAsync(BuildUri(baseUrl, relativeUrl), value, JsonOptions));
    }

    private async Task<HttpResponseMessage> SendWithFallbackAsync(Func<string, Task<HttpResponseMessage>> send)
    {
        Exception? lastError = null;
        foreach (var baseUrl in _backendBaseUrls)
        {
            try
            {
                var response = await send(baseUrl);
                var statusCode = (int)response.StatusCode;
                if (response.IsSuccessStatusCode || statusCode is >= 400 and < 500)
                {
                    return response;
                }

                lastError = new HttpRequestException($"API returned {(int)response.StatusCode} from {baseUrl}.");
                response.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                System.Diagnostics.Debug.WriteLine($"API base URL failed: {baseUrl} - {ex.Message}");
            }
        }

        throw new HttpRequestException($"Cannot reach TourGuide API. Last error: {lastError?.Message}", lastError);
    }

    private static Uri BuildUri(string baseUrl, string relativeUrl)
    {
        return new Uri(new Uri(baseUrl), relativeUrl);
    }

    private static PoiModel NormalizePoi(PoiModel poi)
    {
        if (poi.Location == null && (poi.Latitude != 0 || poi.Longitude != 0))
        {
            poi.Location = new GeoLocation
            {
                Coordinates = new[] { poi.Longitude, poi.Latitude },
            };
        }

        return poi;
    }

    private static PoiModel MapPublicDetail(PoiPublicDetailResponse detail)
    {
        var model = new PoiModel
        {
            Id = detail.Id,
            Name = detail.Name,
            Address = detail.Address,
            Tags = detail.Tags.ToList(),
            ImageUrl = detail.ImageUrl,
            Latitude = detail.Latitude,
            Longitude = detail.Longitude,
            Location = new GeoLocation { Coordinates = new[] { detail.Longitude, detail.Latitude } },
            Description_VI = GetContent(detail, "VI").Description,
            Description_EN = GetContent(detail, "EN").Description,
            Description_KO = GetContent(detail, "KO").Description,
            Description_JA = GetContent(detail, "JA").Description,
            Description_ZH = GetContent(detail, "ZH").Description,
            AudioUrl_VI = GetContent(detail, "VI").AudioUrl,
            AudioUrl_EN = GetContent(detail, "EN").AudioUrl,
            AudioUrl_KO = GetContent(detail, "KO").AudioUrl,
            AudioUrl_JA = GetContent(detail, "JA").AudioUrl,
            AudioUrl_ZH = GetContent(detail, "ZH").AudioUrl,
        };

        model.SourceDescription = model.Description_VI
            ?? model.Description_EN
            ?? model.Description_KO
            ?? model.Description_JA
            ?? model.Description_ZH;

        return model;
    }

    private static LocalizedContent GetContent(PoiPublicDetailResponse detail, string language)
    {
        return detail.Contents.TryGetValue(language, out var content)
            ? content
            : new LocalizedContent();
    }

    private static IReadOnlyList<string> ResolveBackendBaseUrls()
    {
        var configured = Preferences.Default.Get("BackendBaseUrl", string.Empty);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new[] { EnsureTrailingSlash(configured.Trim()) };
        }

        if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
        {
            return new[]
            {
                "http://10.0.2.2:5276/",
                "https://10.0.2.2:7095/",
            };
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return new[]
            {
                "https://localhost:7095/",
                "http://localhost:5276/",
            };
        }

        return new[] { "https://localhost:7095/" };
    }

    private static HttpMessageHandler CreateHttpMessageHandler()
    {
#if DEBUG
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
#else
        return new HttpClientHandler();
#endif
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
    }

    private static string EnsurePreference(string key, string prefix)
    {
        var value = Preferences.Default.Get(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = $"{prefix}-{Guid.NewGuid():N}";
        Preferences.Default.Set(key, value);
        return value;
    }

    private sealed class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
    }

    private sealed class PoiPublicDetailResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string ImageUrl { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Dictionary<string, LocalizedContent> Contents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LocalizedContent
    {
        public string Description { get; set; } = string.Empty;
        public string AudioUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

public sealed class QrScanResult
{
    public bool Counted { get; set; }
    public bool InCooldown { get; set; }
    public DateTime CooldownEndsAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class NarrationPlayResult
{
    public string LogId { get; set; } = string.Empty;
    public bool Counted { get; set; }
    public bool RateLimited { get; set; }
}
