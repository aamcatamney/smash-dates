using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Services.Notifications;

// Resolves recipients for domain events and enqueues outbox notifications. Delivery is a
// separate (deferred) concern; this only records what should be sent.
public interface INotificationService
{
    Task MembershipInvitedAsync(Guid clubId, Guid leagueId, CancellationToken ct = default);
    Task MembershipRespondedAsync(Guid clubId, Guid leagueId, MembershipStatus status, CancellationToken ct = default);
    Task MatchStatusChangedAsync(Guid matchId, MatchStatus status, CancellationToken ct = default);

    // Player registration / transfer events.
    Task RegistrationRequestedAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, CancellationToken ct = default);
    Task RegistrationRespondedAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, RegistrationStatus status, CancellationToken ct = default);
    Task TransferOpenedAsync(Guid playerId, Guid fromClubId, Guid toClubId, Guid leagueId, Discipline discipline, CancellationToken ct = default);
    Task TransferResolvedAsync(Guid playerId, Guid fromClubId, Guid toClubId, Guid leagueId, Discipline discipline, bool completed, CancellationToken ct = default);
}

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _outbox;
    private readonly IClubRepository _clubs;
    private readonly ILeagueRepository _leagues;
    private readonly ILeagueAdminRepository _leagueAdmins;
    private readonly IUserRepository _users;
    private readonly ITeamRepository _teams;
    private readonly IMatchRepository _matches;
    private readonly IPlayerRepository _players;

    public NotificationService(
        INotificationRepository outbox,
        IClubRepository clubs,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IUserRepository users,
        ITeamRepository teams,
        IMatchRepository matches,
        IPlayerRepository players)
    {
        _outbox = outbox;
        _clubs = clubs;
        _leagues = leagues;
        _leagueAdmins = leagueAdmins;
        _users = users;
        _teams = teams;
        _matches = matches;
        _players = players;
    }

    private async Task NotifyLeagueAdminsAsync(Guid leagueId, string subject, string body, CancellationToken ct)
    {
        foreach (var grant in await _leagueAdmins.ListByLeagueAsync(leagueId, ct))
        {
            var user = await _users.GetByIdAsync(grant.UserId, ct);
            if (user is not null) await _outbox.EnqueueAsync(user.Email, subject, body, ct);
        }
    }

    public async Task RegistrationRequestedAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        var club = await _clubs.GetByIdAsync(clubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (player is null || club is null || league is null) return;

        await NotifyLeagueAdminsAsync(
            leagueId,
            $"Registration awaiting confirmation — {league.Name}",
            $"{club.Name} has requested to register {player.FullName} for {discipline} in {league.Name}.",
            ct);
    }

    public async Task RegistrationRespondedAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, RegistrationStatus status, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        var club = await _clubs.GetByIdAsync(clubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (player is null || club is null || league is null) return;

        await _outbox.EnqueueAsync(
            club.ContactEmail,
            $"Registration {status} — {league.Name}",
            $"{player.FullName}'s {discipline} registration in {league.Name} was {status.ToString().ToLowerInvariant()}.",
            ct);
    }

    public async Task TransferOpenedAsync(Guid playerId, Guid fromClubId, Guid toClubId, Guid leagueId, Discipline discipline, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        var from = await _clubs.GetByIdAsync(fromClubId, ct);
        var to = await _clubs.GetByIdAsync(toClubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (player is null || from is null || to is null || league is null) return;

        var subject = $"Transfer requested — {player.FullName} ({discipline})";
        var body = $"{to.Name} has requested to transfer {player.FullName}'s {discipline} registration from {from.Name} in {league.Name}. The releasing club and the league must approve.";
        await _outbox.EnqueueAsync(from.ContactEmail, subject, body, ct);
        await NotifyLeagueAdminsAsync(leagueId, subject, body, ct);
    }

    public async Task TransferResolvedAsync(Guid playerId, Guid fromClubId, Guid toClubId, Guid leagueId, Discipline discipline, bool completed, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        var from = await _clubs.GetByIdAsync(fromClubId, ct);
        var to = await _clubs.GetByIdAsync(toClubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (player is null || from is null || to is null || league is null) return;

        var outcome = completed ? "completed" : "rejected";
        var subject = $"Transfer {outcome} — {player.FullName} ({discipline})";
        var body = $"The transfer of {player.FullName}'s {discipline} registration from {from.Name} to {to.Name} in {league.Name} was {outcome}.";
        await _outbox.EnqueueAsync(from.ContactEmail, subject, body, ct);
        if (to.ContactEmail != from.ContactEmail) await _outbox.EnqueueAsync(to.ContactEmail, subject, body, ct);
    }

    public async Task MembershipInvitedAsync(Guid clubId, Guid leagueId, CancellationToken ct = default)
    {
        var club = await _clubs.GetByIdAsync(clubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (club is null || league is null) return;

        await _outbox.EnqueueAsync(
            club.ContactEmail,
            $"Invitation to join {league.Name}",
            $"{club.Name} has been invited to join {league.Name}. A club admin can accept or decline the invitation.",
            ct);
    }

    public async Task MembershipRespondedAsync(Guid clubId, Guid leagueId, MembershipStatus status, CancellationToken ct = default)
    {
        var club = await _clubs.GetByIdAsync(clubId, ct);
        var league = await _leagues.GetByIdAsync(leagueId, ct);
        if (club is null || league is null) return;

        // Membership responses are league business — notify the league's admins.
        foreach (var grant in await _leagueAdmins.ListByLeagueAsync(leagueId, ct))
        {
            var user = await _users.GetByIdAsync(grant.UserId, ct);
            if (user is null) continue;
            await _outbox.EnqueueAsync(
                user.Email,
                $"Membership {status} — {league.Name}",
                $"{club.Name} has {status.ToString().ToLowerInvariant()} its membership in {league.Name}.",
                ct);
        }
    }

    public async Task MatchStatusChangedAsync(Guid matchId, MatchStatus status, CancellationToken ct = default)
    {
        var match = await _matches.GetByIdAsync(matchId, ct);
        if (match is null) return;

        var home = await _teams.GetByIdAsync(match.HomeTeamId, ct);
        var away = await _teams.GetByIdAsync(match.AwayTeamId, ct);
        if (home is null || away is null) return;

        var homeClub = await _clubs.GetByIdAsync(home.ClubId, ct);
        var awayClub = await _clubs.GetByIdAsync(away.ClubId, ct);
        if (homeClub is null || awayClub is null) return;

        var subject = $"Match {status} — {home.Name} v {away.Name}";
        var body = $"The match {home.Name} v {away.Name} on {match.MatchDate:yyyy-MM-dd} is now {status}.";

        await _outbox.EnqueueAsync(homeClub.ContactEmail, subject, body, ct);
        // A derby has both teams at one club — don't notify the same contact twice.
        if (awayClub.ContactEmail != homeClub.ContactEmail)
            await _outbox.EnqueueAsync(awayClub.ContactEmail, subject, body, ct);
    }
}
