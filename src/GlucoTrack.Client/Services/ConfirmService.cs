namespace GlucoTrack.Client.Services;

public class ConfirmService
{
    public event Action? OnChanged;

    public string? Title   { get; private set; }
    public string? Message { get; private set; }
    public bool    IsOpen  { get; private set; }

    private TaskCompletionSource<bool>? _tcs;

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Title   = title;
        Message = message;
        IsOpen  = true;
        _tcs    = new TaskCompletionSource<bool>();
        OnChanged?.Invoke();
        return _tcs.Task;
    }

    public void Resolve(bool result)
    {
        IsOpen = false;
        OnChanged?.Invoke();
        _tcs?.TrySetResult(result);
    }
}
