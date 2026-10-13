using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.SharedInfrastructure.Persistence;

/// <summary>
/// Classifies provider-specific persistence failures without treating every
/// database update error as an expected business conflict.
/// </summary>
public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueConstraintViolation(
        this DbUpdateException exception,
        params string[] constraintNames)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(constraintNames);

        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } postgresException)
        {
            return false;
        }

        if (constraintNames.Length == 0)
        {
            return true;
        }

        return postgresException.ConstraintName is string constraintName &&
            constraintNames.Contains(
                constraintName,
                StringComparer.Ordinal);
    }

    public static bool IsExclusionConstraintViolation(
        this DbUpdateException exception,
        params string[] constraintNames)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(constraintNames);

        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.ExclusionViolation
            } postgresException)
        {
            return false;
        }

        return constraintNames.Length == 0 ||
            postgresException.ConstraintName is string constraintName &&
            constraintNames.Contains(
                constraintName,
                StringComparer.Ordinal);
    }

    public static bool HasPostgresSqlState(
        this DbUpdateException exception,
        string sqlState)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlState);
        return exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == sqlState;
    }
}
