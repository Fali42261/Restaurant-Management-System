using Microsoft.Data.SqlClient;
namespace RestaurantManagement.API.Repositories;
public interface ISqlConnectionFactory{SqlConnection CreateConnection();}
public sealed class SqlConnectionFactory(IConfiguration configuration):ISqlConnectionFactory{public SqlConnection CreateConnection(){var cs=configuration["AppSettings:Database:ConnectionString"];if(string.IsNullOrWhiteSpace(cs))throw new InvalidOperationException("Database connection string is missing.");return new SqlConnection(cs);}}