using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class GlucoseEndpoints
{
    public static IEndpointRouteBuilder MapGlucoseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/glucose").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(DateTime? from, DateTime? to, AppDbContext db)
    {
        var query = db.GlucoseReadings.AsQueryable();
        if (from.HasValue) query = query.Where(e => e.MeasuredAtUtc >= from.Value);
        if (to.HasValue)   query = query.Where(e => e.MeasuredAtUtc <= to.Value);
        return Results.Ok(await query.OrderBy(e => e.MeasuredAtUtc).ToListAsync());
    }

    private static async Task<IResult> CreateAsync(GlucoseRequest req, AppDbContext db, ICurrentUserService currentUser)
    {
        var entry = new GlucoseReading
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            MeasuredAtUtc = req.MeasuredAtUtc,
            ValueMmol = req.ValueMmol,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.GlucoseReadings.Add(entry);
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> UpdateAsync(Guid id, GlucoseRequest req, AppDbContext db)
    {
        var entry = await db.GlucoseReadings.FindAsync(id);
        if (entry is null) return Results.NotFound();
        entry.MeasuredAtUtc = req.MeasuredAtUtc;
        entry.ValueMmol = req.ValueMmol;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(entry);
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var entry = await db.GlucoseReadings.FindAsync(id);
        if (entry is null) return Results.NotFound();
        entry.IsDeleted = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private record GlucoseRequest(DateTime MeasuredAtUtc, double ValueMmol);
}
