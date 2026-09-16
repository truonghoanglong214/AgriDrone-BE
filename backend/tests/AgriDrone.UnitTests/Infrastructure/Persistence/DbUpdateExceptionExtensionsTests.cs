using AgriDrone.SharedInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Persistence;

public sealed class DbUpdateExceptionExtensionsTests
{
    [Fact]
    public void IsUniqueConstraintViolationReturnsTrueForMatchingConstraint()
    {
        var exception = CreateDatabaseException(
            PostgresErrorCodes.UniqueViolation,
            "uq_test_code");

        Assert.True(exception.IsUniqueConstraintViolation(
            "uq_test_code"));
    }

    [Fact]
    public void IsUniqueConstraintViolationReturnsFalseForDifferentConstraint()
    {
        var exception = CreateDatabaseException(
            PostgresErrorCodes.UniqueViolation,
            "uq_test_code");

        Assert.False(exception.IsUniqueConstraintViolation(
            "uq_other_code"));
    }

    [Fact]
    public void IsUniqueConstraintViolationReturnsFalseForNonUniqueFailure()
    {
        var exception = CreateDatabaseException(
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_test_parent");

        Assert.False(exception.IsUniqueConstraintViolation(
            "fk_test_parent"));
    }

    private static DbUpdateException CreateDatabaseException(
        string sqlState,
        string constraintName)
    {
        var postgresException = new PostgresException(
            "Database constraint violation.",
            "ERROR",
            "ERROR",
            sqlState,
            constraintName: constraintName);

        return new DbUpdateException(
            "Database update failed.",
            postgresException);
    }
}
