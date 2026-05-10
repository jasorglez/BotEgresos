using EgresosBot.Api.Application.Models.BotLinks;

namespace EgresosBot.Api.Application.Abstractions;

public interface IBotLinkService
{
    Task<IReadOnlyCollection<BotLinkResponse>> GetUserLinksAsync(int tenantId, int userId, CancellationToken cancellationToken = default);
    Task<CreateBotLinkCodeResponse> CreateLinkCodeAsync(int tenantId, int userId, CreateBotLinkCodeRequest request, CancellationToken cancellationToken = default);
    Task<BotLinkResponse?> LinkByCodeAsync(LinkBotChannelRequest request, CancellationToken cancellationToken = default);
}
