using System.Runtime.CompilerServices;
using Dapper;

namespace smash_dates.Data;

internal static class DapperConfiguration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
