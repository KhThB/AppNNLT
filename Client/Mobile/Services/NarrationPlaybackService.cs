using Mobile.Models;

namespace Mobile.Services;

public sealed class NarrationPlaybackService
{
    private readonly PoiService _poiService;
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private CancellationTokenSource? _currentPlaybackCts;

    public NarrationPlaybackService(PoiService poiService)
    {
        _poiService = poiService;
    }

    public async Task StopAsync()
    {
        _currentPlaybackCts?.Cancel();
        await StopPlatformAudioAsync();
    }

    public async Task<NarrationPlaybackResult> PlayAsync(
        PoiModel poi,
        string language,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poi.Id))
        {
            return NarrationPlaybackResult.Failed("POI chưa có mã định danh.");
        }

        await _playbackLock.WaitAsync(cancellationToken);
        var logId = string.Empty;
        var startedAt = DateTime.UtcNow;
        _currentPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var play = await _poiService.StartNarrationAsync(poi.Id, language);
            if (play == null)
            {
                return NarrationPlaybackResult.Failed("Không ghi nhận được lượt nghe.");
            }

            logId = play.LogId;
            var audioUrl = ResolveAudioUrl(poi, language);
            var usedAudio = false;

            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                usedAudio = await TryPlayAudioAsync(audioUrl, _currentPlaybackCts.Token);
            }

            if (!usedAudio)
            {
                await SpeakWithTtsAsync(text, language, _currentPlaybackCts.Token);
            }

            await FinishAsync(logId, startedAt, "Completed");
            return new NarrationPlaybackResult
            {
                Success = true,
                RateLimited = play.RateLimited,
                UsedAudioFile = usedAudio,
            };
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrWhiteSpace(logId))
            {
                await FinishAsync(logId, startedAt, "Stopped");
            }

            return NarrationPlaybackResult.Failed("Đã dừng thuyết minh.");
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(logId))
            {
                await FinishAsync(logId, startedAt, "Error", "PLAYBACK_FAILED");
            }

            return NarrationPlaybackResult.Failed(ex.Message);
        }
        finally
        {
            _currentPlaybackCts?.Dispose();
            _currentPlaybackCts = null;
            _playbackLock.Release();
        }
    }

    private async Task FinishAsync(string logId, DateTime startedAt, string status, string errorCode = "")
    {
        var dwell = Math.Max(0, (int)Math.Round((DateTime.UtcNow - startedAt).TotalSeconds));
        await _poiService.FinishNarrationAsync(logId, status, dwell, errorCode);
    }

    private static string ResolveAudioUrl(PoiModel poi, string language)
    {
        return language.ToUpperInvariant() switch
        {
            "EN" => poi.AudioUrl_EN ?? string.Empty,
            "KO" => poi.AudioUrl_KO ?? string.Empty,
            "JA" => poi.AudioUrl_JA ?? string.Empty,
            "ZH" => poi.AudioUrl_ZH ?? string.Empty,
            _ => poi.AudioUrl_VI ?? string.Empty,
        };
    }

    private static async Task SpeakWithTtsAsync(string text, string language, CancellationToken cancellationToken)
    {
        var locales = await TextToSpeech.Default.GetLocalesAsync();
        var localePrefix = language.ToUpperInvariant() switch
        {
            "EN" => "en",
            "KO" => "ko",
            "JA" => "ja",
            "ZH" => "zh",
            _ => "vi",
        };

        var locale = locales.FirstOrDefault(l => l.Language.StartsWith(localePrefix, StringComparison.OrdinalIgnoreCase));
        var options = locale == null ? new SpeechOptions() : new SpeechOptions { Locale = locale };
        await TextToSpeech.Default.SpeakAsync(text, options, cancelToken: cancellationToken);
    }

    private static Task StopPlatformAudioAsync()
    {
#if ANDROID
        AndroidAudioPlayback.Stop();
#endif
        return Task.CompletedTask;
    }

    private static Task<bool> TryPlayAudioAsync(string audioUrl, CancellationToken cancellationToken)
    {
#if ANDROID
        return AndroidAudioPlayback.PlayAsync(audioUrl, cancellationToken);
#else
        return Task.FromResult(false);
#endif
    }
}

public sealed class NarrationPlaybackResult
{
    public bool Success { get; set; }
    public bool RateLimited { get; set; }
    public bool UsedAudioFile { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static NarrationPlaybackResult Failed(string message)
    {
        return new NarrationPlaybackResult { ErrorMessage = message };
    }
}

#if ANDROID
internal static class AndroidAudioPlayback
{
    private static Android.Media.MediaPlayer? _player;

    public static void Stop()
    {
        try
        {
            _player?.Stop();
            _player?.Release();
        }
        catch
        {
        }
        finally
        {
            _player = null;
        }
    }

    public static async Task<bool> PlayAsync(string audioUrl, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        Stop();

        try
        {
            var player = new Android.Media.MediaPlayer();
            _player = player;

            player.Prepared += (_, _) => player.Start();
            player.Completion += (_, _) =>
            {
                Stop();
                tcs.TrySetResult(true);
            };
            player.Error += (_, args) =>
            {
                args.Handled = true;
                Stop();
                tcs.TrySetResult(false);
            };

            using var registration = cancellationToken.Register(() =>
            {
                Stop();
                tcs.TrySetCanceled(cancellationToken);
            });

            player.SetDataSource(audioUrl);
            player.PrepareAsync();
            return await tcs.Task;
        }
        catch
        {
            Stop();
            return false;
        }
    }
}
#endif
