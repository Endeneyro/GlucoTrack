using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Shared.Events;

/// <summary>
/// Правила согласования плановых событий с реальными записями: что прятать в дневнике
/// (чтобы не было дублей) и какие связанные записи удалять каскадом.
/// </summary>
public static class EventReconciler
{
    /// <summary>
    /// Id записей питания, которые нужно скрыть в дневнике, потому что они созданы из
    /// выполненного планового события (показывается само событие, а не дубль-запись).
    /// </summary>
    public static HashSet<Guid> HiddenMealEntryIds(
        IEnumerable<MealEntryDto> meals,
        IEnumerable<PlannedEventDto> plannedEvents)
    {
        var doneEventIds = plannedEvents
            .Where(e => !e.IsDeleted && e.IsDone)
            .Select(e => e.Id)
            .ToHashSet();

        return meals
            .Where(m => m.LinkedEventId.HasValue && doneEventIds.Contains(m.LinkedEventId.Value))
            .Select(m => m.Id)
            .ToHashSet();
    }

    /// <summary>Id записей питания, созданных из данного планового события (каскад: событие → записи).</summary>
    public static List<Guid> LinkedMealIds(Guid eventId, IEnumerable<MealEntryDto> meals) =>
        meals.Where(m => !m.IsDeleted && m.LinkedEventId == eventId)
             .Select(m => m.Id)
             .ToList();

    /// <summary>Id планового события, из которого создана запись питания (каскад: запись → событие), или null.</summary>
    public static Guid? LinkedEventId(MealEntryDto meal) => meal.LinkedEventId;

    /// <summary>
    /// Id записей (глюкоза / инсулин), которые нужно скрыть в дневнике — созданы из выполненного
    /// планового события либо попали в ±<paramref name="windowMinutes"/> от него (фоллбэк для старых записей без связи).
    /// </summary>
    public static HashSet<Guid> HiddenRecordIds<T>(
        IEnumerable<T> records,
        Func<T, Guid> getId,
        Func<T, Guid?> getLinkedEventId,
        Func<T, DateTime> getTimeUtc,
        IEnumerable<PlannedEventDto> doneEvents,
        double windowMinutes = 15)
    {
        var recordList = records.ToList();
        var result = new HashSet<Guid>();
        var doneList = doneEvents.Where(e => !e.IsDeleted && e.IsDone).ToList();
        var doneEventIds = doneList.Select(e => e.Id).ToHashSet();

        // 1) Явная ссылка: прячем записи, указывающие на выполненное событие, и помечаем такие
        //    события «покрытыми» — для них проксимити-фоллбэк уже не нужен.
        var coveredEventIds = new HashSet<Guid>();
        foreach (var r in recordList)
        {
            var linkedId = getLinkedEventId(r);
            if (linkedId.HasValue && doneEventIds.Contains(linkedId.Value))
            {
                result.Add(getId(r));
                coveredEventIds.Add(linkedId.Value);
            }
        }

        // 2) Проксимити-фоллбэк (для старых записей без LinkedEventId) — только для событий,
        //    которые ещё не покрыты явной ссылкой, и прячем РОВНО ОДНУ ближайшую запись в окне.
        //    Иначе другой, более поздний замер рядом с выполненным событием ошибочно скрывается.
        foreach (var ev in doneList)
        {
            if (coveredEventIds.Contains(ev.Id)) continue;

            T? best = default;
            double bestDiff = double.MaxValue;
            foreach (var r in recordList)
            {
                if (result.Contains(getId(r))) continue; // уже скрыта другой ссылкой/событием
                var diff = Math.Abs((getTimeUtc(r) - ev.PlannedAtUtc).TotalMinutes);
                if (diff <= windowMinutes && diff < bestDiff)
                {
                    bestDiff = diff;
                    best = r;
                }
            }
            if (best is not null)
                result.Add(getId(best));
        }
        return result;
    }

    /// <summary>
    /// Id ближайшей записи в окне ±<paramref name="windowMinutes"/> от времени события — реальная
    /// запись, «покрывающая» выполненное событие замера/инъекции. null, если в окне ничего нет.
    /// </summary>
    public static Guid? CoveredRecordId<T>(
        IEnumerable<T> records,
        Func<T, Guid> getId,
        Func<T, DateTime> getTimeUtc,
        DateTime eventTimeUtc,
        double windowMinutes = 15)
    {
        T? best = default;
        double bestDiff = double.MaxValue;
        foreach (var r in records)
        {
            var diff = Math.Abs((getTimeUtc(r) - eventTimeUtc).TotalMinutes);
            if (diff <= windowMinutes && diff < bestDiff)
            {
                bestDiff = diff;
                best = r;
            }
        }
        return best is null ? null : getId(best);
    }
}
