using EgresosBot.Api.Application.Models.Auth;

namespace EgresosBot.Api.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default);
}
