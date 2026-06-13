namespace GlucoTrack.Client.Services;

public class UndoService
{
    public event Action? OnChanged;

    public UndoEntry? Current { get; private set; }

    private CancellationTokenSource? _cts;

    public void Push(string label, Func<Task> undoAction)
    {
        _cts?.Cancel();
        Current = new UndoEntry(label, undoAction);
        OnChanged?.Invoke();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000, token);
            if (!token.IsCancellationRequested) Expire();
        }, token).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
    }

    public async Task UndoAsync()
    {
        if (Current is null) return;
        var action = Current.UndoAction;
        Expire();
        await action();
    }

    public void Dismiss() => Expire();

    private void Expire()
    {
        _cts?.Cancel();
        Current = null;
        OnChanged?.Invoke();
    }
}

public record UndoEntry(string Label, Func<Task> UndoAction);
