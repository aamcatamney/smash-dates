namespace smash_dates.Services.Import;

// One parsed data row. Fields are keyed by lower-cased header so Get() is case-insensitive.
// LineNumber is the 1-based source line where the record begins (header = line 1) so import
// errors can point the user at the right row.
public sealed record CsvRow(int LineNumber, IReadOnlyDictionary<string, string> Fields)
{
    public string Get(string column) => Fields.TryGetValue(column.ToLowerInvariant(), out var v) ? v : "";
}

public sealed class CsvDocument
{
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<CsvRow> Rows { get; init; }
}

// Minimal RFC-4180 CSV reader (no external dependency). Handles quoted fields containing
// commas, newlines and escaped "" quotes, a UTF-8 BOM, CRLF/LF line endings, blank lines,
// and ragged rows. Values and headers are trimmed.
public static class CsvParser
{
    public static CsvDocument Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new CsvDocument { Headers = [], Rows = [] };

        if (text[0] == '﻿')
            text = text[1..];

        var records = ReadRecords(text);

        // First non-empty record is the header.
        var headerIndex = records.FindIndex(r => r.Fields.Any(f => f.Length > 0));
        if (headerIndex < 0)
            return new CsvDocument { Headers = [], Rows = [] };

        var header = records[headerIndex];
        var headers = header.Fields.Select(h => h.Trim()).ToList();
        var keys = headers.Select(h => h.ToLowerInvariant()).ToList();

        var rows = new List<CsvRow>();
        for (var i = headerIndex + 1; i < records.Count; i++)
        {
            var rec = records[i];
            // Skip wholly-blank lines.
            if (rec.Fields.All(f => f.Trim().Length == 0))
                continue;

            var map = new Dictionary<string, string>(keys.Count);
            for (var c = 0; c < keys.Count; c++)
            {
                var value = c < rec.Fields.Count ? rec.Fields[c].Trim() : "";
                map[keys[c]] = value;
            }
            rows.Add(new CsvRow(rec.LineNumber, map));
        }

        return new CsvDocument { Headers = headers, Rows = rows };
    }

    private sealed record RawRecord(int LineNumber, List<string> Fields);

    private static List<RawRecord> ReadRecords(string text)
    {
        var records = new List<RawRecord>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var line = 1;
        var recordStartLine = 1;
        var inQuotes = false;
        var recordHasContent = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            records.Add(new RawRecord(recordStartLine, fields.ToList()));
            fields.Clear();
            recordHasContent = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (ch == '\n') line++;
                    field.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    recordHasContent = true;
                    break;
                case ',':
                    EndField();
                    recordHasContent = true;
                    break;
                case '\r':
                    break; // handled by the following \n (or ignored)
                case '\n':
                    EndRecord();
                    line++;
                    recordStartLine = line;
                    break;
                default:
                    field.Append(ch);
                    recordHasContent = true;
                    break;
            }
        }

        // Flush the final record if the file did not end with a newline.
        if (recordHasContent || field.Length > 0 || fields.Count > 0)
            EndRecord();

        return records;
    }
}
