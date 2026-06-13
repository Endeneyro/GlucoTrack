using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meals").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(DateOnly? date, AppDbContext db)
    {
        var query = db.MealEntries.AsQueryable();
        if (date.HasValue)
            query = query.Where(e => e.Date == date.Value);
        return Results.Ok(await query.OrderBy(e => e.Date).ThenBy(e => e.Time).ToListAsync());
    }

    private static async Task<IResult> CreateAsync(MealEntryRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var entry = new MealEntry
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            Date = req.Date,
            Time = req.Time,
            MealType = req.MealType,
            ProductId = req.ProductId,
            Grams = req.Grams,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.MealEntries.Add(entry);
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> UpdateAsync(Guid id, MealEntryRequest req, AppDbContext db)
    {
        var entry = await db.MealEntries.FindAsync(id);
        if (entry is null) return Results.NotFound();

        entry.Date = req.Date; entry.Time = req.Time;
        entry.MealType = req.MealType; entry.ProductId = req.ProductId;
        entry.Grams = req.Grams; entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var entry = await db.MealEntries.FindAsync(id);
        if (entry is null) return Results.NotFound();
        entry.IsDeleted = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private record MealEntryRequest(DateOnly Date, TimeOnly Time, MealType MealType, Guid ProductId, double Grams);
}
