using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;

namespace RestaurantManagement.API.Infrastructure;

public sealed class SqlDataAccess(SqlConnectionFactory factory, IOptions<AppSettings> options)
{
    private readonly int _timeout = options.Value.Database.CommandTimeoutSeconds;

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync();
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
        await using var connection = factory.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0) command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync();
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);
}

    public async Task<List<Dictionary<string, object?>>> QueryProcedureAsync(string procedureName, params SqlParameter[] parameters)
    {
        if (string.IsNullOrWhiteSpace(procedureName) || procedureName.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '.')))
            throw new ArgumentException("Invalid stored procedure name.", nameof(procedureName));

        await using var connection = factory.Create();
        await connection.OpenAsync();
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
