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

    public NotificationService(
        INotificationRepository outbox,
        IClubRepository clubs,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IUserRepository users,
        ITeamRepository teams,
        IMatchRepository matches)
    {
        _outbox = outbox;
        _clubs = clubs;
        _leagues = leagues;
        _leagueAdmins = leagueAdmins;
        _users = users;
        _teams = teams;
        _matches = matches;
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
