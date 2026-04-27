using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Abstractions;

public interface IAnalyticsService
{
    Task RecordPingAsync(PingRequest request, CancellationToken cancellationToken = default);
    int GetActiveUserCount();
    Task<QrScanResponse> RecordQrScanAsync(QrScanRequest request, CancellationToken cancellationToken = default);
    Task<NarrationPlayResponse> StartNarrationAsync(NarrationPlayRequest request, CancellationToken cancellationToken = default);
    Task FinishNarrationAsync(NarrationFinishRequest request, CancellationToken cancellationToken = default);
    Task<AdminOverviewResponse> GetAdminOverviewAsync(CancellationToken cancellationToken = default);
    Task<OwnerOverviewResponse> GetOwnerOverviewAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(int hours, CancellationToken cancellationToken = default);
}
