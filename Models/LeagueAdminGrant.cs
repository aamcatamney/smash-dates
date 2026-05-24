namespace smash_dates.Models;

public sealed class LeagueAdminGrant
{
    public Guid LeagueId { get; init; }
    public Guid UserId { get; init; }
    public DateTime GrantedAt { get; init; }
    public Guid? GrantedBy { get; init; }
}
