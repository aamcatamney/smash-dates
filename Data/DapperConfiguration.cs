using System.Data;
using System.Runtime.CompilerServices;
using Dapper;

namespace smash_dates.Data;

internal static class DapperConfiguration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        // Dapper doesn't bind DateOnly/TimeOnly as parameters out of the box, though Npgsql maps
        // them to `date`/`time` natively. These handlers pass the value straight through (Npgsql
        // accepts it) and read it back unchanged.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => DateOnly.Parse((string)value),
        };
    }

    private sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
    {
        public override void SetValue(IDbDataParameter parameter, TimeOnly value)
        {
            parameter.DbType = DbType.Time;
            parameter.Value = value;
        }

        public override TimeOnly Parse(object value) => value switch
        {
            TimeOnly t => t,
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),
            DateTime dt => TimeOnly.FromDateTime(dt),
            _ => TimeOnly.Parse((string)value),
        };
    }
}
