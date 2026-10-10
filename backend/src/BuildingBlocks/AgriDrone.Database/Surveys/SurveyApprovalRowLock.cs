using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal static class SurveyApprovalRowLock
{
    public static async Task<bool> ExistsAsync(
        SurveyApprovalDbContext context,
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        context.EnsureApprovalTransaction();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction!
            .GetDbTransaction();

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    public static NpgsqlParameter Id(string name, Guid value) =>
        new(name, value);
}
