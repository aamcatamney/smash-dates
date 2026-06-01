using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

// A waiting attendee considered for a lineup. Order = position in the wait queue (0 = longest waiting).
public sealed record FillCandidate(Guid Id, Gender Gender, int? Grade, int Order);

// A proposed lineup split into two sides.
public sealed record FillSuggestion(IReadOnlyList<Guid> SideA, IReadOnlyList<Guid> SideB);
