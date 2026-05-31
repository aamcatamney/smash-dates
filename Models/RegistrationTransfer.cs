namespace smash_dates.Models;

// Moves a Confirmed registration between clubs within one (league, discipline). Initiated
// by the receiving club; releasing club + league must both approve. See docs/adr/0003.
public sealed class RegistrationTransfer
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Guid LeagueId { get; init; }
    public Discipline Discipline { get; init; }
    public Guid FromClubId { get; init; }
    public Guid ToClubId { get; init; }
    public TransferStatus Status { get; init; }
    public bool ReleasingApproved { get; init; }
    public bool LeagueApproved { get; init; }
    public Guid? InitiatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
}
