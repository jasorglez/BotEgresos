using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.BotLinks;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class BotLinkService(EgresosBotDbContext dbContext) : IBotLinkService
{
    public async Task<IReadOnlyCollection<BotLinkResponse>> GetUserLinksAsync(int tenantId, int userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BotLinks
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .OrderBy(x => x.Channel)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateBotLinkCodeResponse> CreateLinkCodeAsync(int tenantId, int userId, CreateBotLinkCodeRequest request, CancellationToken cancellationToken = default)
    {
        var channel = NormalizeChannel(request.Channel);
        var now = DateTime.UtcNow;
        var linkCode = $"{channel[..Math.Min(3, channel.Length)]}-{Guid.NewGuid():N}"[..15].ToUpperInvariant();

        var existing = await dbContext.BotLinks
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Channel == channel, cancellationToken);

        if (existing is null)
        {
            existing = new BotLink
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = channel,
                LinkCode = linkCode,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.BotLinks.Add(existing);
        }
        else
        {
            existing.LinkCode = linkCode;
            existing.LinkedAt = null;
            existing.ExternalChatId = null;
            existing.ExternalUserId = null;
            existing.PhoneNumber = null;
            existing.Username = null;
            existing.IsActive = true;
            existing.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateBotLinkCodeResponse
        {
            Channel = channel,
            LinkCode = linkCode,
            ExpiresAtUtc = now.AddHours(24)
        };
    }

    public async Task<BotLinkResponse?> LinkByCodeAsync(LinkBotChannelRequest request, CancellationToken cancellationToken = default)
    {
        var channel = NormalizeChannel(request.Channel);
        var linkCode = request.LinkCode.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var entity = await dbContext.BotLinks
            .FirstOrDefaultAsync(x => x.Channel == channel && x.LinkCode == linkCode && x.IsActive, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.ExternalUserId = string.IsNullOrWhiteSpace(request.ExternalUserId) ? null : request.ExternalUserId.Trim();
        entity.ExternalChatId = request.ExternalChatId.Trim();
        entity.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        entity.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        entity.LinkedAt = now;
        entity.LinkCode = null;
        entity.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private static string NormalizeChannel(string value)
    {
        var channel = value.Trim().ToUpperInvariant();
        return channel switch
        {
            "TELEGRAM" => channel,
            "WHATSAPP" => channel,
            _ => throw new InvalidOperationException("Canal no soportado.")
        };
    }

    private static BotLinkResponse Map(BotLink entity)
    {
        return new BotLinkResponse
        {
            Id = entity.Id,
            Channel = entity.Channel,
            ExternalUserId = entity.ExternalUserId,
            ExternalChatId = entity.ExternalChatId,
            PhoneNumber = entity.PhoneNumber,
            Username = entity.Username,
            IsActive = entity.IsActive,
            LinkedAtUtc = entity.LinkedAt
        };
    }
}
