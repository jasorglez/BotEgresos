using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Tenants;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class TenantService(
    EgresosBotDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher) : ITenantService
{
    public async Task<IReadOnlyCollection<TenantSummaryResponse>> GetUserTenantsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TenantUsers
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.IsOwner)
            .ThenBy(x => x.Tenant.Name)
            .Select(x => new TenantSummaryResponse
            {
                TenantId = x.TenantId,
                PublicId = x.Tenant.PublicId,
                Name = x.Tenant.Name,
                Slug = x.Tenant.Slug,
                RoleCode = x.RoleCode,
                IsOwner = x.IsOwner
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RegisterTenantResponse> RegisterAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default)
    {
        var companyName = request.CompanyName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();

        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new InvalidOperationException("El nombre de la empresa es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("El correo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidOperationException("El nombre del usuario es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new InvalidOperationException("La contraseña debe tener al menos 8 caracteres.");
        }

        var emailExists = await dbContext.Users
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException("Ese correo ya está registrado.");
        }

        var slug = await BuildUniqueSlugAsync(request.Slug, companyName, cancellationToken);
        var now = DateTime.UtcNow;

        var tenant = new Tenant
        {
            PublicId = Guid.NewGuid(),
            Name = companyName,
            Slug = slug,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Status = "TRIAL",
            TrialEndsAt = now.AddDays(14),
            CreatedAt = now,
            UpdatedAt = now
        };

        var user = new AppUser
        {
            PublicId = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var tenantUser = new TenantUser
        {
            Tenant = tenant,
            User = user,
            RoleCode = "OWNER",
            IsOwner = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        dbContext.TenantUsers.Add(tenantUser);

        dbContext.ExpenseCategories.AddRange(
            new ExpenseCategory
            {
                Tenant = tenant,
                Name = "General",
                Description = "Categoria general inicial",
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ExpenseCategory
            {
                Tenant = tenant,
                Name = "Operación",
                Description = "Gastos operativos",
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterTenantResponse
        {
            TenantId = tenant.Id,
            TenantPublicId = tenant.PublicId,
            CompanyName = tenant.Name,
            Slug = tenant.Slug,
            UserId = user.Id,
            UserPublicId = user.PublicId,
            Email = user.Email,
            RoleCode = tenantUser.RoleCode
        };
    }

    private async Task<string> BuildUniqueSlugAsync(string? requestedSlug, string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = NormalizeSlug(string.IsNullOrWhiteSpace(requestedSlug) ? companyName : requestedSlug);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = $"empresa-{Guid.NewGuid():N}"[..14];
        }

        var slug = baseSlug;
        var suffix = 2;

        while (await dbContext.Tenants.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static string NormalizeSlug(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
