using GlucoTrack.Shared.DTOs.Sync;
using GlucoTrack.Shared.Events;

namespace GlucoTrack.Shared.Tests;

public class EventReconcilerTests
{
    private static readonly DateTime T = new(2026, 6, 13, 15, 0, 0, DateTimeKind.Utc);

    private static PlannedEventDto Event(Guid id, bool done, bool deleted = false, int type = (int)PlannedEventType.Meal) =>
        new(id, T, type, null, null, null, done, T, deleted);

    private static MealEntryDto Meal(Guid id, Guid? linked) =>
        new(id, DateOnly.FromDateTime(T), TimeOnly.FromDateTime(T), (int)0, Guid.NewGuid(), 1, T, false, linked);

    [Fact]
    public void DoneEvent_HidesOnlyItsLinkedMeal()
    {
        var ev = Guid.NewGuid();
        var linkedMeal = Guid.NewGuid();
        var otherMeal = Guid.NewGuid();

        var hidden = EventReconciler.HiddenMealEntryIds(
            new[] { Meal(linkedMeal, ev), Meal(otherMeal, null) },
            new[] { Event(ev, done: true) });

        Assert.Contains(linkedMeal, hidden);
        Assert.DoesNotContain(otherMeal, hidden);
    }

    [Fact]
    public void NotDoneEvent_HidesNothing()
    {
        var ev = Guid.NewGuid();
        var meal = Guid.NewGuid();

        var hidden = EventReconciler.HiddenMealEntryIds(
            new[] { Meal(meal, ev) },
            new[] { Event(ev, done: false) });

        Assert.Empty(hidden);
    }

    [Fact]
    public void DeletedDoneEvent_HidesNothing()
    {
        var ev = Guid.NewGuid();
        var meal = Guid.NewGuid();

        var hidden = EventReconciler.HiddenMealEntryIds(
            new[] { Meal(meal, ev) },
            new[] { Event(ev, done: true, deleted: true) });

        Assert.Empty(hidden);
    }

    [Fact]
    public void TwoMeals_OneEvent_BothHidden()
    {
        var ev = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        var hidden = EventReconciler.HiddenMealEntryIds(
            new[] { Meal(m1, ev), Meal(m2, ev) },
            new[] { Event(ev, done: true) });

        Assert.Equal(2, hidden.Count);
    }

    [Fact]
    public void Cascade_EventToMeals_FindsLinked()
    {
        var ev = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var meals = new[] { Meal(m1, ev), Meal(Guid.NewGuid(), Guid.NewGuid()), Meal(Guid.NewGuid(), null) };

        var linked = EventReconciler.LinkedMealIds(ev, meals);

        Assert.Equal(new[] { m1 }, linked);
    }

    [Fact]
    public void Cascade_MealToEvent_ReturnsLinkedId()
    {
        var ev = Guid.NewGuid();
        Assert.Equal(ev, EventReconciler.LinkedEventId(Meal(Guid.NewGuid(), ev)));
        Assert.Null(EventReconciler.LinkedEventId(Meal(Guid.NewGuid(), null)));
    }

    [Fact]
    public void CoveredRecordId_PicksNearestWithinWindow()
    {
        var near = new GlucoseReadingDto(Guid.NewGuid(), T.AddMinutes(5), 7, T, false);
        var far  = new GlucoseReadingDto(Guid.NewGuid(), T.AddMinutes(40), 8, T, false);

        var id = EventReconciler.CoveredRecordId(
            new[] { far, near }, r => r.Id, r => r.MeasuredAtUtc, T, windowMinutes: 15);

        Assert.Equal(near.Id, id);
    }

    [Fact]
    public void CoveredRecordId_BoundaryInclusive()
    {
        var at15 = new GlucoseReadingDto(Guid.NewGuid(), T.AddMinutes(15), 7, T, false);
        var id = EventReconciler.CoveredRecordId(
            new[] { at15 }, r => r.Id, r => r.MeasuredAtUtc, T, windowMinutes: 15);
        Assert.Equal(at15.Id, id);
    }

    [Fact]
    public void CoveredRecordId_OutsideWindow_Null()
    {
        var at16 = new GlucoseReadingDto(Guid.NewGuid(), T.AddMinutes(16), 7, T, false);
        var id = EventReconciler.CoveredRecordId(
            new[] { at16 }, r => r.Id, r => r.MeasuredAtUtc, T, windowMinutes: 15);
        Assert.Null(id);
    }

    // ── HiddenRecordIds (glucose/insulin) ──────────────────────────────────────

    private static GlucoseReadingDto Reading(Guid id, DateTime time, Guid? linked = null) =>
        new(id, time, 7.0, T, false, linked);

    private static PlannedEventDto GlucoseEvent(Guid id, bool done, DateTime? at = null) =>
        new(id, at ?? T, (int)PlannedEventType.Glucose, null, null, null, done, T, false);

    [Fact]
    public void HiddenRecordIds_LinkedToDoneEvent_Hidden()
    {
        var evId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        var hidden = EventReconciler.HiddenRecordIds(
            new[] { Reading(recId, T.AddMinutes(60), evId) }, // far from event time
            r => r.Id, r => r.LinkedEventId, r => r.MeasuredAtUtc,
            new[] { GlucoseEvent(evId, done: true) });

        Assert.Contains(recId, hidden);
    }

    [Fact]
    public void HiddenRecordIds_LinkedToDoneEvent_FarInTime_StillHidden()
    {
        var evId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        // 90 minutes away — would fail ±15 min heuristic but explicit link must win
        var hidden = EventReconciler.HiddenRecordIds(
            new[] { Reading(recId, T.AddMinutes(90), evId) },
            r => r.Id, r => r.LinkedEventId, r => r.MeasuredAtUtc,
            new[] { GlucoseEvent(evId, done: true) });

        Assert.Contains(recId, hidden);
    }

    [Fact]
    public void HiddenRecordIds_LinkedToNotDoneEvent_NotHidden()
    {
        var evId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        var hidden = EventReconciler.HiddenRecordIds(
            new[] { Reading(recId, T, evId) },
            r => r.Id, r => r.LinkedEventId, r => r.MeasuredAtUtc,
            new[] { GlucoseEvent(evId, done: false) });

        Assert.Empty(hidden);
    }

    [Fact]
    public void HiddenRecordIds_NoLink_FallbackProximityWindow_Hidden()
    {
        var evId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        // no LinkedEventId, but within 15 min
        var hidden = EventReconciler.HiddenRecordIds(
            new[] { Reading(recId, T.AddMinutes(10), null) },
            r => r.Id, r => r.LinkedEventId, r => r.MeasuredAtUtc,
            new[] { GlucoseEvent(evId, done: true) });

        Assert.Contains(recId, hidden);
    }

    [Fact]
    public void HiddenRecordIds_NoLink_OutsideWindow_NotHidden()
    {
        var evId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        var hidden = EventReconciler.HiddenRecordIds(
            new[] { Reading(recId, T.AddMinutes(20), null) },
            r => r.Id, r => r.LinkedEventId, r => r.MeasuredAtUtc,
            new[] { GlucoseEvent(evId, done: true) });

        Assert.Empty(hidden);
    }
}
