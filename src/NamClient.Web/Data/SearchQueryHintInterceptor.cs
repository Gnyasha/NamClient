using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NamClient.Web.Data;

/// <summary>Search terms vary hugely in selectivity, so tagged search queries get a per-term plan without the TOP row-goal guess.</summary>
public sealed class SearchQueryHintInterceptor : DbCommandInterceptor
{
    public const string Tag = "search-query-hints";

    private const string Hints = "\nOPTION (RECOMPILE, USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))";

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        AddHints(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        AddHints(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        AddHints(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        AddHints(command);
        return ValueTask.FromResult(result);
    }

    private static void AddHints(DbCommand command)
    {
        if (command.CommandText.StartsWith("-- " + Tag, StringComparison.Ordinal) &&
            !command.CommandText.EndsWith(Hints, StringComparison.Ordinal))
        {
            command.CommandText += Hints;
        }
    }
}
