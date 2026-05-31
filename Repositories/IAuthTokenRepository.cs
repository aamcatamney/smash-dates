namespace smash_dates.Repositories;

// One-time, expiring auth tokens (password reset, email verification). Stores only hashes.
public interface IAuthTokenRepository
{
    // Issues a token for the user + purpose, returning the RAW token to put in the emailed link.
    Task<string> IssueAsync(Guid userId, string purpose, TimeSpan ttl, CancellationToken ct = default);

    // Validates and consumes (marks used) a raw token for the purpose. Returns the user id if
    // the token is valid, unused and unexpired; null otherwise.
    Task<Guid?> ConsumeAsync(string rawToken, string purpose, CancellationToken ct = default);
}
