using System.Data;
using Dapper;

namespace smash_dates.Data;

// Dapper (this version) cannot bind DateOnly as a command parameter. Convert to a
// DbType.Date DateTime on write; on read accept whatever Npgsql hands back.
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        string s => DateOnly.Parse(s),
        _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly"),
    };
}
