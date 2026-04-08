using System.Data.Common;

namespace DataDesensitization.Services;

public interface IDbProviderFactory
{
    DbConnection CreateConnection(string connectionString);
}
