using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GlucoTrack.Api.Data;
using GlucoTrack.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GlucoTrack.Api.Services;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public TokenService(IConfiguration config, AppDbContext db, UserManager<AppUser> userManager)
    {
        _config = config;
        _db = db;
        _userManager = userManager;
    }

    public async Task<AuthResponse> CreateTokensAsync(AppUser user)
    {
        var accessToken = await GenerateAccessTokenAsync(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        var expiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes());
        return new AuthResponse(accessToken, refreshToken, expiresAt);
    }

    public async Task<string> GenerateAccessTokenAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();
        return token;
    }

    private int GetAccessTokenMinutes() =>
        int.TryParse(_config["Jwt:AccessTokenMinutes"], out var m) ? m : 60;
}
