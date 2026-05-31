using Dapper;
using smash_dates.Data;
using smash_dates.Services.Auth;

namespace smash_dates.Repositories;

public sealed class AuthTokenRepository : IAuthTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public AuthTokenRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<string> IssueAsync(Guid userId, string purpose, TimeSpan ttl, CancellationToken ct = default)
    {
        var token = AuthTokenCodec.NewToken();
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO auth_tokens (user_id, purpose, token_hash, expires_at)
                  VALUES (@userId, @purpose, @hash, now() + @ttl)",
                new { userId, purpose, hash = AuthTokenCodec.Hash(token), ttl },
                cancellationToken: ct));
        return token;
    }

    public async Task<Guid?> ConsumeAsync(string rawToken, string purpose, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                @"UPDATE auth_tokens SET used_at = now()
                  WHERE token_hash = @hash AND purpose = @purpose AND used_at IS NULL AND expires_at > now()
                  RETURNING user_id",
                new { hash = AuthTokenCodec.Hash(rawToken), purpose },
                cancellationToken: ct));
    }
}
