using GlucoTrack.Client.Models;

namespace GlucoTrack.Client.Services;

public class ToastItem
{
    public Guid EventId { get; }
    public PlannedEventDto Event { get; }
    public bool IsExpanded { get; set; }
    public ToastItem(PlannedEventDto ev) { EventId = ev.Id; Event = ev; }
}

public class ToastService : IAsyncDisposable
{
    private readonly DbService _db;
    private PeriodicTimer? _timer;
    private Task? _timerTask;
    private readonly List<ToastItem> _toasts = [];
    private readonly HashSet<Guid> _shown = [];

    public IReadOnlyList<ToastItem> Toasts => _toasts;
    public event Action? OnChanged;

    public ToastService(DbService db)
    {
        _db = db;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _timerTask = RunTimerAsync();
    }

    private async Task RunTimerAsync()
    {
        if (_timer is null) return;
        // initial check
        await CheckDueEventsAsync();
        while (await _timer.WaitForNextTickAsync())
            await CheckDueEventsAsync();
    }

    private async Task CheckDueEventsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var events = await _db.GetAllAsync<PlannedEventDto>("planned_events");
            var due = events.Where(e =>
                !e.IsDeleted &&
                !e.IsDone &&
                !_shown.Contains(e.Id) &&
                (e.PlannedAtUtc - now).TotalMinutes is >= -2 and <= 1
            ).ToList();

            if (due.Count == 0) return;

            foreach (var ev in due)
            {
                _shown.Add(ev.Id);
                _toasts.Add(new ToastItem(ev));
            }
            OnChanged?.Invoke();
        }
        catch { /* swallow — runs in background */ }
    }

    public void Dismiss(Guid eventId)
    {
        _toasts.RemoveAll(t => t.EventId == eventId);
        OnChanged?.Invoke();
    }

    public async Task MarkDoneAsync(Guid eventId)
    {
        var events = await _db.GetAllAsync<PlannedEventDto>("planned_events");
        var ev = events.FirstOrDefault(e => e.Id == eventId);
        if (ev is not null)
            await _db.PutAsync("planned_events", ev with { IsDone = true, UpdatedAtUtc = DateTime.UtcNow }, pending: false);
        Dismiss(eventId);
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _timer = null;
        if (_timerTask is not null)
            await _timerTask.ConfigureAwait(false);
    }
}
