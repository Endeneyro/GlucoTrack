using Microsoft.JSInterop;

namespace GlucoTrack.Client.Services;

public class ConnectivityService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ConnectivityService>? _selfRef;

    public bool IsOnline { get; private set; } = true;
    public event Action? OnChanged;

    public ConnectivityService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        IsOnline = await _js.InvokeAsync<bool>("glucoDb.isOnline");
        _selfRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("registerConnectivityEvents", _selfRef);
    }

    [JSInvokable]
    public void OnOnline()
    {
        IsOnline = true;
        OnChanged?.Invoke();
    }

    [JSInvokable]
    public void OnOffline()
    {
        IsOnline = false;
        OnChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
