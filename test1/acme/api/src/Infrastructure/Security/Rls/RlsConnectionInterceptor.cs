using System.Data.Common;
using Acme.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Acme.Infrastructure.Security.Rls;

/// <summary>
/// Sets the PostgreSQL session variable <c>app.user_id</c> to the authenticated
/// user on every connection open, so Row-Level Security policies scope queries to
/// that user. When there is no authenticated user the value is cleared, and the
/// policies (which match on a non-null id) return no rows.
/// </summary>
public sealed class RlsConnectionInterceptor(ICurrentUser currentUser) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        => await ApplyAsync(connection, cancellationToken);

    private void Apply(DbConnection connection)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.user_id', @uid, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "uid";
        parameter.Value = currentUser.UserId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }
}
