using System.Security.Claims;
using GlucoTrack.Api.Data;
using GlucoTrack.Api.Services;
using GlucoTrack.Shared.DTOs.Auth;
using GlucoTrack.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GlucoTrack.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/change-password", ChangePassword).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest req,
        UserManager<AppUser> userManager,
        AppDbContext db,
        TokenService tokenService)
    {
        var invite = await db.InviteCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Code == req.InviteCode && i.UsedAtUtc == null);

        if (invite is null)
            return Results.Problem("Неверный или использованный инвайт-код.", statusCode: 400);

        var user = new AppUser { UserName = req.Email, Email = req.Email };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        invite.UsedAtUtc = DateTime.UtcNow;
        invite.UsedByUserId = user.Id;
        await db.SaveChangesAsync();

        var tokens = await tokenService.CreateTokensAsync(user);
        return Results.Ok(tokens);
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        UserManager<AppUser> userManager,
        TokenService tokenService)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, req.Password))
            return Results.Problem("Неверный email или пароль.", statusCode: 401);

        var tokens = await tokenService.CreateTokensAsync(user);
        return Results.Ok(tokens);
    }

    private static async Task<IResult> Refresh(
        RefreshRequest req,
        AppDbContext db,
        UserManager<AppUser> userManager,
        TokenService tokenService)
    {
        var stored = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsRevoked);

        if (stored is null || stored.ExpiresAtUtc < DateTime.UtcNow)
            return Results.Problem("Refresh token недействителен.", statusCode: 401);

        stored.IsRevoked = true;
        await db.SaveChangesAsync();

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return Results.Problem("Пользователь не найден.", statusCode: 401);

        var tokens = await tokenService.CreateTokensAsync(user);
        return Results.Ok(tokens);
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest req,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await userManager.FindByIdAsync(userId!);
        if (user is null)
            return Results.Problem("Пользователь не найден.", statusCode: 404);

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        return Results.Ok();
    }
}
