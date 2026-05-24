using System.Data;

namespace smash_dates.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
