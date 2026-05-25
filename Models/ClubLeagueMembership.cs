namespace smash_dates.Models;

public sealed class ClubLeagueMembership
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid LeagueId { get; init; }
    public MembershipStatus Status { get; init; }
    public DateTime InvitedAt { get; init; }
    public Guid? InvitedBy { get; init; }
    public DateTime? RespondedAt { get; init; }
    public Guid? RespondedBy { get; init; }
}
