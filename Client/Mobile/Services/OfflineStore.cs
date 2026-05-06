using System.Text.Json;
using Mobile.Models;
using SQLite;

namespace Mobile.Services;

public sealed class OfflineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SQLiteAsyncConnection _database;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public OfflineStore()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "tourguide_offline.db3");
        _database = new SQLiteAsyncConnection(path);
    }

    public async Task SavePoisAsync(IEnumerable<PoiModel> pois)
    {
        await InitializeAsync();
        foreach (var poi in pois)
        {
            await SavePoiAsync(poi);
        }
    }

    public async Task SavePoiAsync(PoiModel poi)
    {
        await InitializeAsync();
        if (string.IsNullOrWhiteSpace(poi.Id))
        {
            return;
        }

        await _database.InsertOrReplaceAsync(PoiCacheEntity.FromModel(poi));
    }

    public async Task<List<PoiModel>> GetApprovedPoisAsync()
    {
        await InitializeAsync();
        var rows = await _database.Table<PoiCacheEntity>()
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task<PoiModel?> GetPoiAsync(string id)
    {
        await InitializeAsync();
        var row = await _database.Table<PoiCacheEntity>()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync();
        return row?.ToModel();
    }

    public async Task EnqueueEventAsync(string eventType, string relativeUrl, object payload)
    {
        await InitializeAsync();
        await _database.InsertAsync(new PendingEventEntity
        {
            EventType = eventType,
            RelativeUrl = relativeUrl,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0,
        });
    }

    public async Task<List<PendingEventEntity>> GetPendingEventsAsync(int take = 50)
    {
        await InitializeAsync();
        return await _database.Table<PendingEventEntity>()
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task DeletePendingEventAsync(int id)
    {
        await InitializeAsync();
        await _database.DeleteAsync<PendingEventEntity>(id);
    }

    public async Task MarkPendingEventAttemptAsync(PendingEventEntity entity, string error)
    {
        await InitializeAsync();
        entity.AttemptCount += 1;
        entity.LastError = error;
        entity.LastAttemptAt = DateTime.UtcNow;
        await _database.UpdateAsync(entity);
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await _database.CreateTableAsync<PoiCacheEntity>();
            await _database.CreateTableAsync<PendingEventEntity>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public sealed class PendingEventEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string RelativeUrl { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public int AttemptCount { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    private sealed class PoiCacheEntity
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string Json { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }

        public static PoiCacheEntity FromModel(PoiModel poi)
        {
            return new PoiCacheEntity
            {
                Id = poi.Id ?? string.Empty,
                Json = JsonSerializer.Serialize(poi, JsonOptions),
                UpdatedAt = poi.UpdatedAt ?? DateTime.UtcNow,
            };
        }

        public PoiModel ToModel()
        {
            return JsonSerializer.Deserialize<PoiModel>(Json, JsonOptions) ?? new PoiModel();
        }
    }
}
