using System.Text.Json;
using System.Text.RegularExpressions;
using Mobile.Models;
using Mobile.Services;
using ZXing.Net.Maui;

namespace Mobile.Views;

public partial class QrScannerPage : ContentPage
{
    private static readonly Regex ObjectIdRegex = new("^[a-fA-F0-9]{24}$", RegexOptions.Compiled);
    private readonly PoiService _poiService;

    public QrScannerPage(PoiService poiService)
    {
        InitializeComponent();
        _poiService = poiService;

        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false,
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        barcodeReader.IsDetecting = true;
        AnimateScanLine();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        barcodeReader.IsDetecting = false;
    }

    private void AnimateScanLine()
    {
        _ = ScanLine.TranslateTo(0, 240, 2000, Easing.Linear).ContinueWith(async _ =>
        {
            if (!barcodeReader.IsDetecting)
            {
                return;
            }

            await ScanLine.TranslateTo(0, 0, 0);
            AnimateScanLine();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var first = e.Results?.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        barcodeReader.IsDetecting = false;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            lblResult.Text = "Đang xử lý dữ liệu...";

            try
            {
                var poiId = TryExtractPoiId(first.Value);
                if (string.IsNullOrWhiteSpace(poiId))
                {
                    await ShowRetryAsync("Mã QR không hợp lệ", "Đây không phải mã QR của hệ thống TourGuide.");
                    return;
                }

                var poi = await _poiService.GetPoiByIdAsync(poiId);
                if (poi == null)
                {
                    await ShowRetryAsync("Lỗi", "Không tìm thấy thông tin quán ăn này trên hệ thống.");
                    return;
                }

                var scan = await _poiService.RecordQrScanAsync(poiId);
                SaveQrHistory(poi);

                if (scan?.InCooldown == true && !string.IsNullOrWhiteSpace(scan.Message))
                {
                    await DisplayAlert("Đã quét gần đây", scan.Message, "Vẫn nghe lại");
                }

                await Shell.Current.GoToAsync(nameof(PoiDetailPage), true, new Dictionary<string, object>
                {
                    { "Poi", poi },
                });
            }
            catch (Exception ex)
            {
                await ShowRetryAsync("Lỗi xử lý", ex.Message);
            }
        });
    }

    private async Task ShowRetryAsync(string title, string message)
    {
        await DisplayAlert(title, message, "Thử lại");
        barcodeReader.IsDetecting = true;
        lblResult.Text = "Hãy hướng Camera vào mã QR của quán";
    }

    private static string? TryExtractPoiId(string qrText)
    {
        var value = qrText.Trim();
        if (ObjectIdRegex.IsMatch(value))
        {
            return value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var queryId = TryReadQueryId(uri.Query);
            if (!string.IsNullOrWhiteSpace(queryId))
            {
                return queryId;
            }

            var segment = uri.Segments
                .Select(x => x.Trim('/'))
                .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return string.IsNullOrWhiteSpace(segment) ? null : segment;
        }

        var markerIndex = value.IndexOf("id=", StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var candidate = value[(markerIndex + 3)..]
                .Split(new[] { '&', '#', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return value.Split(new[] { '/', '?', '&', '#' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(x => ObjectIdRegex.IsMatch(x));
    }

    private static string? TryReadQueryId(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    private static void SaveQrHistory(PoiModel poi)
    {
        if (string.IsNullOrWhiteSpace(poi.Id))
        {
            return;
        }

        var json = Preferences.Default.Get("QrHistory", "[]");
        var list = JsonSerializer.Deserialize<List<QrHistoryModel>>(json) ?? new List<QrHistoryModel>();
        list.RemoveAll(x => x.PoiId == poi.Id);
        list.Insert(0, new QrHistoryModel
        {
            PoiId = poi.Id,
            PoiName = poi.Name ?? "POI",
            ScanTime = DateTime.Now,
        });

        Preferences.Default.Set("QrHistory", JsonSerializer.Serialize(list.Take(50).ToList()));
    }
}
