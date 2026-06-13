using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class InsulinEndpoints
{
    public static IEndpointRouteBuilder MapInsulinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insulin").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(DateTime? from, DateTime? to, AppDbContext db)
    {
        var query = db.InsulinInjections.AsQueryable();
        if (from.HasValue) query = query.Where(e => e.InjectedAtUtc >= from.Value);
        if (to.HasValue)   query = query.Where(e => e.InjectedAtUtc <= to.Value);
        return Results.Ok(await query.OrderByDescending(e => e.InjectedAtUtc).ToListAsync());
    }

    private static async Task<IResult> CreateAsync(InsulinRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var entry = new InsulinInjection
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            InjectedAtUtc = req.InjectedAtUtc,
            Units = req.Units,
            InsulinType = req.InsulinType,
            Carbs = req.Carbs,
            GlucoseBefore = req.GlucoseBefore,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.InsulinInjections.Add(entry);
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> UpdateAsync(Guid id, InsulinRequest req, AppDbContext db)
    {
        var entry = await db.InsulinInjections.FindAsync(id);
        if (entry is null) return Results.NotFound();
        entry.InjectedAtUtc = req.InjectedAtUtc; entry.Units = req.Units;
        entry.InsulinType = req.InsulinType; entry.Carbs = req.Carbs;
        entry.GlucoseBefore = req.GlucoseBefore; entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var entry = await db.InsulinInjections.FindAsync(id);
        if (entry is null) return Results.NotFound();
        entry.IsDeleted = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private record InsulinRequest(
        DateTime InjectedAtUtc, double Units, InsulinType InsulinType,
        double? Carbs, double? GlucoseBefore);
}
