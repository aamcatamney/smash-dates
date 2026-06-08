namespace smash_dates.Models;

// A user's role grant joined to the league/club it names — for the read-only "my grants"
// view on the profile page. Distinct from the *Grant models (which carry the raw join row
// with user/granted-by) because here the grantee is implied and the name is what matters.
public sealed record LeagueAdminGrantView(Guid LeagueId, string LeagueName);

public sealed record ClubAdminGrantView(Guid ClubId, string ClubName);

public sealed record SessionHostGrantView(Guid ClubId, string ClubName);
