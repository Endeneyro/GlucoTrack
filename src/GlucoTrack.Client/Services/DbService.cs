using System.Text.Json;
using Microsoft.JSInterop;

namespace GlucoTrack.Client.Services;

/// <summary>
/// Wraps the IndexedDB JS layer. Uses camelCase serialization so JS keyPath 'id' matches.
/// </summary>
public class DbService
{
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DbService(IJSRuntime js) => _js = js;

    public async Task PutAsync<T>(string store, T item, bool pending = true)
    {
        var json = JsonSerializer.Serialize(item, WriteOpts);
        await _js.InvokeVoidAsync("glucoDb.putJson", store, json, pending);
    }

    public async Task<List<T>> GetAllAsync<T>(string store)
    {
        var json = await _js.InvokeAsync<string>("glucoDb.getAllJson", store);
        return JsonSerializer.Deserialize<List<T>>(json, ReadOpts) ?? [];
    }

    public async Task<List<T>> GetPendingAsync<T>(string store)
    {
        var json = await _js.InvokeAsync<string>("glucoDb.getPendingJson", store);
        return JsonSerializer.Deserialize<List<T>>(json, ReadOpts) ?? [];
    }

    public async Task MarkSyncedAsync(string store, Guid id)
        => await _js.InvokeVoidAsync("glucoDb.markSynced", store, id.ToString());

    public async Task BulkPutAsync<T>(string store, IEnumerable<T> items)
    {
        var json = JsonSerializer.Serialize(items, WriteOpts);
        await _js.InvokeVoidAsync("glucoDb.bulkPutJson", store, json);
    }

    public async Task<long> GetLastSyncTicksAsync()
    {
        var val = await _js.InvokeAsync<string>("glucoDb.getLastSyncTicks");
        return long.TryParse(val, out var ticks) ? ticks : 0;
    }

    public async Task SetLastSyncTicksAsync(long ticks)
        => await _js.InvokeVoidAsync("glucoDb.setLastSyncTicks", ticks);

    public async Task<bool> IsOnlineAsync()
        => await _js.InvokeAsync<bool>("glucoDb.isOnline");

    public async Task ClearAllAsync()
        => await _js.InvokeVoidAsync("glucoDb.clearAll");
}
