using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;

namespace RestaurantManagement.API.Infrastructure;

/// <summary>
/// Central SQL Server access. Connections are short-lived and returned to the ADO.NET
/// connection pool after each operation. Every command is asynchronous and cancellable.
/// </summary>
public sealed class SqlDataAccess(
    SqlConnectionFactory factory,
    IOptions<AppSettings> options,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly int _timeout = Math.Clamp(options.Value.Database.CommandTimeoutSeconds, 5, 300);

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = factory.Create();
        try
        {
            await connection.OpenAsync(cancellationToken);

            var principal = httpContextAccessor.HttpContext?.User;
            var restaurantId = principal?.FindFirst("restaurant_id")?.Value;
            var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? principal?.FindFirst("sub")?.Value;

            // Set both RLS context values in one database round-trip per connection.
            await using var command = new SqlCommand(
                """
                EXEC sys.sp_set_session_context @key=N'RestaurantId', @value=@RestaurantId;
                EXEC sys.sp_set_session_context @key=N'UserId', @value=@UserId;
                """,
                connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = _timeout
            };

            command.Parameters.Add(new SqlParameter("@RestaurantId", SqlDbType.UniqueIdentifier)
            {
                Value = Guid.TryParse(restaurantId, out var restaurantGuid)
                    ? restaurantGuid
                    : DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.UniqueIdentifier)
            {
                Value = Guid.TryParse(userId, out var userGuid)
                    ? userGuid
                    : DBNull.Value
            });

            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params SqlParameter[] parameters) =>
        QueryAsync(sql, CancellationToken.None, parameters);

    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0)
            command.Parameters.AddRange(parameters);

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, cancellationToken);
        return await ReadRowsAsync(reader, cancellationToken);
    }

    public Task<int> ExecuteAsync(string sql, params SqlParameter[] parameters) =>
        ExecuteAsync(sql, CancellationToken.None, parameters);

    public async Task<int> ExecuteAsync(
        string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0)
            command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<object?> ScalarAsync(string sql, params SqlParameter[] parameters) =>
        ScalarAsync(sql, CancellationToken.None, parameters);

    public async Task<object?> ScalarAsync(
        string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        if (parameters.Length > 0)
            command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public static SqlParameter P(string name, object? value) =>
        new(name, value ?? DBNull.Value);

    public static SqlParameter P(string name, SqlDbType type, object? value, int? size = null)
    {
        var parameter = size.HasValue
            ? new SqlParameter(name, type, size.Value)
            : new SqlParameter(name, type);
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    public Task<int> ExecuteProcedureAsync(string procedureName, params SqlParameter[] parameters) =>
        ExecuteProcedureAsync(procedureName, CancellationToken.None, parameters);

    public async Task<int> ExecuteProcedureAsync(
        string procedureName, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        ValidateProcedureName(procedureName);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _timeout
        };
        if (parameters.Length > 0)
            command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<List<Dictionary<string, object?>>> QueryProcedureAsync(string procedureName, params SqlParameter[] parameters) =>
        QueryProcedureAsync(procedureName, CancellationToken.None, parameters);

    public async Task<List<Dictionary<string, object?>>> QueryProcedureAsync(
        string procedureName, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        ValidateProcedureName(procedureName);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _timeout
        };
        if (parameters.Length > 0)
            command.Parameters.AddRange(parameters);

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, cancellationToken);
        return await ReadRowsAsync(reader, cancellationToken);
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        SqlDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        for (var i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < fieldCount; i++)
                row[columnNames[i]] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }

    private static void ValidateProcedureName(string procedureName)
    {
        if (string.IsNullOrWhiteSpace(procedureName) ||
            procedureName.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '.')))
            throw new ArgumentException("Invalid stored procedure name.", nameof(procedureName));
    }
}
