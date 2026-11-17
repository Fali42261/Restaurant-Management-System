using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;

namespace RestaurantManagement.API.Infrastructure;

public sealed class SqlConnectionFactory(IOptions<AppSettings> options)
{
    private readonly string _connectionString = options.Value.Database.ConnectionString;
    public SqlConnection Create() => new(_connectionString);
}