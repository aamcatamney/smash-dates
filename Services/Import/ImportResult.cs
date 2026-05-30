namespace smash_dates.Services.Import;

public sealed record RowError(int Row, string Message);

// Outcome of a partial CSV import: counts of rows that created or updated a record, plus a
// per-row error list for rows that were skipped (validation failures, unresolved references).
public sealed class ImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<RowError> Errors { get; } = [];

    public void Error(int row, string message) => Errors.Add(new RowError(row, message));
}
