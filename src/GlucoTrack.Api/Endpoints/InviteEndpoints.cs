using System.Security.Claims;
using System.Security.Cryptography;
using GlucoTrack.Api.Data;
using GlucoTrack.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class InviteEndpoints
{
    public static void MapInviteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invites").RequireAuthorization();

        group.MapPost("/", CreateInvite);
        group.MapGet("/", ListInvites);
    }

    private static async Task<IResult> CreateInvite(
        ClaimsPrincipal principal,
        AppDbContext db)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var code = GenerateCode();
        db.InviteCodes.Add(new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return Results.Ok(new { code });
    }

    private static async Task<IResult> ListInvites(
        ClaimsPrincipal principal,
        AppDbContext db)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var invites = await db.InviteCodes
            .IgnoreQueryFilters()
            .Where(i => i.CreatedByUserId == userId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new { i.Code, i.CreatedAtUtc, i.UsedAtUtc })
            .ToListAsync();
        return Results.Ok(invites);
    }

    private static string GenerateCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            [..12];
}
