using System.Net.Http.Json;
using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Client.Services;

public class SyncService
{
    private readonly HttpClient _http;
    private readonly DbService _db;
    private readonly ConnectivityService _connectivity;

    public bool IsSyncing { get; private set; }
    public DateTime? LastSyncUtc { get; private set; }
    public int PendingCount { get; private set; }

    public event Action? OnStateChanged;

    public SyncService(HttpClient http, DbService db, ConnectivityService connectivity)
    {
        _http = http;
        _db = db;
        _connectivity = connectivity;

        // Sync automatically when connectivity is restored
        _connectivity.OnChanged += () =>
        {
            if (_connectivity.IsOnline)
                _ = SyncAsync();
        };
    }

    /// <summary>Full sync: push pending changes, then pull server changes.</summary>
    public async Task SyncAsync()
    {
        if (IsSyncing || !_connectivity.IsOnline) return;

        IsSyncing = true;
        NotifyChanged();
        try
        {
            await PushPendingAsync();
            await PullAsync();
            LastSyncUtc = DateTime.UtcNow;
        }
        catch
        {
            // Network error — will retry next time connectivity changes or user triggers manually
        }
        finally
        {
            IsSyncing = false;
            await RefreshPendingCountAsync();
            NotifyChanged();
        }
    }

    /// <summary>Collect pending items from IndexedDB and push to server (last-write-wins).</summary>
    private async Task PushPendingAsync()
    {
        var meals = await _db.GetPendingAsync<MealEntryDto>("meals");
        var glucose = await _db.GetPendingAsync<GlucoseReadingDto>("glucose");
        var insulin = await _db.GetPendingAsync<InsulinInjectionDto>("insulin");
        var products = await _db.GetPendingAsync<ProductDto>("products");
        var therapy = await _db.GetPendingAsync<TherapyCoeffDto>("therapy");
        var settingsList = await _db.GetPendingAsync<UserSettingsDto>("settings");

        bool anyPending = meals.Count + glucose.Count + insulin.Count +
                          products.Count + therapy.Count + settingsList.Count > 0;
        if (!anyPending) return;

        var request = new SyncPushRequest(
            meals, glucose, insulin, products, therapy, settingsList.FirstOrDefault());

        var response = await _http.PostAsJsonAsync("/api/sync/push", request);
        if (!response.IsSuccessStatusCode) return;

        var result = await response.Content.ReadFromJsonAsync<SyncPushResponse>();
        if (result is null) return;

        // Mark synced — skip items the server rejected (conflicts, they'll be overwritten on pull)
        await MarkSyncedBatch("meals", meals.Select(x => x.Id), result.Conflicts);
        await MarkSyncedBatch("glucose", glucose.Select(x => x.Id), result.Conflicts);
        await MarkSyncedBatch("insulin", insulin.Select(x => x.Id), result.Conflicts);
        await MarkSyncedBatch("products", products.Select(x => x.Id), result.Conflicts);
        await MarkSyncedBatch("therapy", therapy.Select(x => x.Id), result.Conflicts);
        if (settingsList.FirstOrDefault() is { } s && !result.Conflicts.Contains(s.Id))
            await _db.MarkSyncedAsync("settings", s.Id);
    }

    private async Task MarkSyncedBatch(string store, IEnumerable<Guid> ids, List<Guid> conflicts)
    {
        foreach (var id in ids)
            if (!conflicts.Contains(id))
                await _db.MarkSyncedAsync(store, id);
    }

    /// <summary>Pull changes from server since last sync and store locally.</summary>
    private async Task PullAsync()
    {
        var sinceTicksStr = await _db.GetLastSyncTicksAsync();
        var response = await _http.GetFromJsonAsync<SyncPullResponse>(
            $"/api/sync/pull?since={sinceTicksStr}");
        if (response is null) return;

        if (response.MealEntries.Count > 0)
            await _db.BulkPutAsync("meals", response.MealEntries);
        if (response.GlucoseReadings.Count > 0)
            await _db.BulkPutAsync("glucose", response.GlucoseReadings);
        if (response.InsulinInjections.Count > 0)
            await _db.BulkPutAsync("insulin", response.InsulinInjections);
        if (response.Products.Count > 0)
            await _db.BulkPutAsync("products", response.Products);
        if (response.TherapyCoefficients.Count > 0)
            await _db.BulkPutAsync("therapy", response.TherapyCoefficients);
        if (response.UserSettings is { } settings)
            await _db.PutAsync("settings", settings, pending: false);

        await _db.SetLastSyncTicksAsync(response.ServerUtc.Ticks);
    }

    public async Task RefreshPendingCountAsync()
    {
        int count = 0;
        foreach (var store in new[] { "meals", "glucose", "insulin", "products", "therapy", "settings" })
            count += (await _db.GetPendingAsync<object>(store)).Count;
        PendingCount = count;
    }

    private void NotifyChanged() => OnStateChanged?.Invoke();
}
