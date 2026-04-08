using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace DataDesensitization.Services;

public class SqlServerProviderFactory : IDbProviderFactory
{
    public DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
