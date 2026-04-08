using System.Data.Common;
using Npgsql;

namespace DataDesensitization.Services;

public class PostgreSqlProviderFactory : IDbProviderFactory
{
    public DbConnection CreateConnection(string connectionString)
    {
        return new NpgsqlConnection(connectionString);
    }
}
