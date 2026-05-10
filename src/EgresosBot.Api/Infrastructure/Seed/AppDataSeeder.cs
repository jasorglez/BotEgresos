using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EgresosBot.Api.Infrastructure.Seed;

public sealed class AppDataSeeder(
    EgresosBotDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<SeedOptions> options,
    ILogger<AppDataSeeder> logger)
{
    private readonly SeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Seed deshabilitado.");
            return;
        }

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("No fue posible conectar a EgresosBotDb. Seed omitido.");
            return;
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Slug == _options.TenantSlug, cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                PublicId = Guid.NewGuid(),
                Name = _options.TenantName,
                Slug = _options.TenantSlug,
                Email = _options.UserEmail,
                Status = "ACTIVE",
                TrialEndsAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == _options.UserEmail, cancellationToken);

        if (user is null)
        {
            user = new AppUser
            {
                PublicId = Guid.NewGuid(),
                FirstName = _options.UserFirstName,
                LastName = _options.UserLastName,
                Email = _options.UserEmail,
                EmailConfirmed = true,
                IsPlatformAdmin = false,
                IsActive = true,
                PasswordAlgorithm = "ASPNET_IDENTITY_V3",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(user, _options.UserPassword);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var tenantUserExists = await dbContext.TenantUsers.AnyAsync(
            x => x.TenantId == tenant.Id && x.UserId == user.Id,
            cancellationToken);

        if (!tenantUserExists)
        {
            dbContext.TenantUsers.Add(new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleCode = _options.RoleCode,
                IsOwner = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var categoryExists = await dbContext.ExpenseCategories.AnyAsync(
            x => x.TenantId == tenant.Id,
            cancellationToken);

        if (!categoryExists)
        {
            dbContext.ExpenseCategories.AddRange(
                new ExpenseCategory
                {
                    TenantId = tenant.Id,
                    Name = "OPERACION",
                    Description = "Gastos operativos generales",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ExpenseCategory
                {
                    TenantId = tenant.Id,
                    Name = "COMBUSTIBLE",
                    Description = "Gastos de combustible y movilidad",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed completado. Usuario demo: {Email} / Password: {Password}",
            _options.UserEmail,
            _options.UserPassword);
    }
}
