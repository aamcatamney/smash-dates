namespace smash_dates.Models;

// A player registered to play a discipline for a club in a league. Scoped to
// (Player, Club, League, Discipline); at most one Confirmed club per (player, league,
// discipline). See docs/adr/0003.
public sealed class DisciplineRegistration
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Guid ClubId { get; init; }
    public Guid LeagueId { get; init; }
    public Discipline Discipline { get; init; }
    public RegistrationStatus Status { get; init; }
    public Guid? RequestedBy { get; init; }
    public Guid? RespondedBy { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? RespondedAt { get; init; }
}
