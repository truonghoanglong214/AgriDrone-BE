using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class RawSqlProjectionSafetyTests
{
    [Fact]
    public void RuntimeSqlDoesNotUseWildcardProjections()
    {
        var sourceRoot = FindBackendRoot()
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single();
        var violations = Directory
            .EnumerateFiles(sourceRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Text = line.Trim()
                }))
            .Where(item => item.Text.Equals(
                "SELECT *",
                StringComparison.OrdinalIgnoreCase))
            .Select(item =>
                $"{Path.GetRelativePath(sourceRoot.FullName, item.Path)}:" +
                $"{item.LineNumber}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Runtime SQL wildcard projections omit PostgreSQL system columns " +
            "such as xmin. Use an alias and explicitly project alias.xmin. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    private static DirectoryInfo FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgriDrone.sln")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the backend solution root.");
    }
}
