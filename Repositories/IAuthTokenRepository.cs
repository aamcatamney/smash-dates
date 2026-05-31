namespace smash_dates.Repositories;

// One-time, expiring auth tokens (password reset, email verification). Stores only hashes.
public interface IAuthTokenRepository
{
    // Issues a token for the user + purpose, returning the RAW token to put in the emailed link.
    // Any prior unused token of the same (user, purpose) is invalidated, so only the latest is live.
    Task<string> IssueAsync(Guid userId, string purpose, TimeSpan ttl, CancellationToken ct = default);

    // Validates and consumes (marks used) a raw token for the purpose. Returns the user id if
    // the token is valid, unused and unexpired; null otherwise.
    Task<Guid?> ConsumeAsync(string rawToken, string purpose, CancellationToken ct = default);

    // Deletes spent tokens (used or past their expiry). Returns the number removed.
    Task<int> DeleteSpentAsync(CancellationToken ct = default);
}
