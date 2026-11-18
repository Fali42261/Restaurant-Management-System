using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;

namespace RestaurantManagement.API.Infrastructure;

public sealed class SqlDataAccess(SqlConnectionFactory factory, IOptions<AppSettings> options, IHttpContextAccessor httpContextAccessor)
{
    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = factory.Create();
        await connection.OpenAsync();
        var principal = httpContextAccessor.HttpContext?.User;
        var restaurantId = principal?.FindFirst("restaurant_id")?.Value;
        var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
        await using var contextCommand = new SqlCommand("sys.sp_set_session_context", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = _timeout };
        contextCommand.Parameters.Add(new SqlParameter("@key", "RestaurantId"));
        contextCommand.Parameters.Add(new SqlParameter("@value", Guid.TryParse(restaurantId, out var restaurantGuid) ? restaurantGuid : DBNull.Value));
        await contextCommand.ExecuteNonQueryAsync();
        await using var userContextCommand = new SqlCommand("sys.sp_set_session_context", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = _timeout };
        userContextCommand.Parameters.Add(new SqlParameter("@key", "UserId"));
        userContextCommand.Parameters.Add(new SqlParameter("@value", Guid.TryParse(userId, out var userGuid) ? userGuid : DBNull.Value));
        await userContextCommand.ExecuteNonQueryAsync();
        return connection;
    }
    private readonly int _timeout = options.Value.Database.CommandTimeoutSeconds;

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<int> ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync();
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);
    public async Task<List<Dictionary<string, object?>>> QueryProcedureAsync(string procedureName, params SqlParameter[] parameters)
    {
        if (string.IsNullOrWhiteSpace(procedureName) || procedureName.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '.')))
            throw new ArgumentException("Invalid stored procedure name.", nameof(procedureName));

        await using var connection = await OpenConnectionAsync();
        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _timeout
        };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}