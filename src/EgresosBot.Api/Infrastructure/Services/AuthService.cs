using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Auth;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Auth;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class AuthService(
    EgresosBotDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var tenantUserQuery = dbContext.TenantUsers
            .Include(x => x.Tenant)
            .Where(x => x.UserId == user.Id && x.IsActive && x.Tenant.Status == "ACTIVE");

        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var tenantSlug = request.TenantSlug.Trim().ToLowerInvariant();
            tenantUserQuery = tenantUserQuery.Where(x => x.Tenant.Slug == tenantSlug);
        }

        var tenantUser = await tenantUserQuery
            .OrderByDescending(x => x.IsOwner)
            .ThenBy(x => x.Tenant.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantUser is null)
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, tenantUser, cancellationToken);
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var token = request.RefreshToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = RefreshTokenFactory.HashToken(token);
        var storedToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.RevokedAt == null &&
                     x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (storedToken is null || !storedToken.User.IsActive || storedToken.Tenant.Status != "ACTIVE")
        {
            return null;
        }

        var tenantUser = await dbContext.TenantUsers
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.TenantId == storedToken.TenantId &&
                     x.UserId == storedToken.UserId &&
                     x.IsActive,
                cancellationToken);

        if (tenantUser is null)
        {
            return null;
        }

        var newRefreshToken = RefreshTokenFactory.CreateToken();
        var newRefreshTokenHash = RefreshTokenFactory.HashToken(newRefreshToken);

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = newRefreshTokenHash;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            TenantId = storedToken.TenantId,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(storedToken.User, tenantUser, newRefreshToken);
    }

    public async Task<bool> RevokeAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        var token = request.RefreshToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenHash = RefreshTokenFactory.HashToken(token);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.RevokedAt == null,
                cancellationToken);

        if (storedToken is null)
        {
            return false;
        }

        storedToken.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<LoginResponse> BuildAuthResponseAsync(AppUser user, TenantUser tenantUser, CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokenFactory.CreateToken();
        var refreshTokenHash = RefreshTokenFactory.HashToken(refreshToken);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TenantId = tenantUser.TenantId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, tenantUser, refreshToken);
    }

    private LoginResponse BuildAuthResponse(AppUser user, TenantUser tenantUser, string refreshToken)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var token = BuildToken(user, tenantUser, expiresAt);

        return new LoginResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Email = user.Email,
            TenantId = tenantUser.TenantId,
            TenantName = tenantUser.Tenant.Name,
            TenantSlug = tenantUser.Tenant.Slug,
            RoleCode = tenantUser.RoleCode
        };
    }

    private string BuildToken(AppUser user, TenantUser tenantUser, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimNames.TenantId, tenantUser.TenantId.ToString()),
            new(ClaimNames.RoleCode, tenantUser.RoleCode)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
