using System.Data;
using Npgsql;

namespace smash_dates.Data;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");
    }

    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
