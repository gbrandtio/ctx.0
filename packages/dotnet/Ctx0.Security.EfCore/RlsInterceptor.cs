using System.Data.Common;
using Ctx0.Security.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Ctx0.Security.EfCore;

/// <summary>
/// Sets the RLS identity context before every command (AUTHORIZATION.md
/// §10, RLS_DEEP_DIVE.md): a dedicated, PARAMETERIZED set_config command
/// on the same connection and transaction — never string interpolation,
/// never prepended SQL. With an ambient transaction the setting is
/// transaction-local (is_local: true) so it can never leak to another
/// pooled request; without one it is session-scoped but re-asserted
/// before every command, so a stale value is always overwritten first.
///
/// System bypass (DATABASE_RLS_POLICIES.md §3): background workers switch
/// to app_internal_worker via SET LOCAL ROLE — which requires the worker
/// to run inside an explicit transaction.
/// </summary>
public sealed class RlsInterceptor(
    ICurrentUserProvider currentUser, IOptions<RlsOptions> options) : DbCommandInterceptor
{
    private readonly RlsOptions _options = options.Value;
    private readonly string _commandPrefix =
        $"SELECT set_config('{options.Value.SettingName}'";

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        ApplyRlsContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await ApplyRlsContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        ApplyRlsContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await ApplyRlsContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        ApplyRlsContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await ApplyRlsContextAsync(command, cancellationToken);
        return result;
    }

    private void ApplyRlsContext(DbCommand command)
    {
        using var rlsCmd = CreateRlsCommand(command);
        if (rlsCmd is not null)
        {
            rlsCmd.ExecuteNonQuery();
        }
    }

    private async Task ApplyRlsContextAsync(DbCommand command, CancellationToken ct)
    {
        await using var rlsCmd = CreateRlsCommand(command);
        if (rlsCmd is not null)
        {
            await rlsCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private DbCommand? CreateRlsCommand(DbCommand command)
    {
        if (IsRlsCommand(command))
        {
            return null; // never recurse into our own context command
        }

        var rlsCmd = command.Connection!.CreateCommand();
        rlsCmd.Transaction = command.Transaction;

        var isLocal = command.Transaction is not null ? "true" : "false";
        if (currentUser.IsSystemBypassActive)
        {
            if (command.Transaction is null)
            {
                rlsCmd.Dispose();
                throw new InvalidOperationException(
                    "System RLS bypass requires an explicit transaction (SET LOCAL ROLE).");
            }
            rlsCmd.CommandText =
                $"SELECT set_config('{_options.SettingName}', @userId, {isLocal}); " +
                $"SET LOCAL ROLE {_options.WorkerRole};";
        }
        else
        {
            rlsCmd.CommandText =
                $"SELECT set_config('{_options.SettingName}', @userId, {isLocal});";
        }

        var parameter = rlsCmd.CreateParameter();
        parameter.ParameterName = "@userId";
        // Empty string → NULLIF(...) in get_current_user_id() yields NULL.
        parameter.Value = currentUser.UserId?.ToString() ?? string.Empty;
        rlsCmd.Parameters.Add(parameter);
        return rlsCmd;
    }

    private bool IsRlsCommand(DbCommand command) =>
        command.CommandText.StartsWith(_commandPrefix, StringComparison.Ordinal);
}
