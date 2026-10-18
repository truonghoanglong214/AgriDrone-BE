using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AgriDrone.Api.Contracts.Surveys.Orders;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class RequestRecordValidationMetadataTests
{
    [Fact]
    public void PositionalRequestRecordsKeepValidationMetadataOnConstructorParameters()
    {
        var apiAssembly = typeof(VerifySurveyOrderBoundaryAndScopeRequest).Assembly;
        var violations = apiAssembly.GetTypes()
            .Where(type =>
                type.IsPublic &&
                type.Name.EndsWith("Request", StringComparison.Ordinal) &&
                type.Namespace?.StartsWith(
                    "AgriDrone.Api.Contracts",
                    StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()
                    .Select(parameter => new
                    {
                        Type = type,
                        Parameter = parameter,
                        Property = type.GetProperty(
                            parameter.Name!,
                            BindingFlags.Public |
                            BindingFlags.Instance |
                            BindingFlags.IgnoreCase)
                    })))
            .Where(item => item.Property is not null)
            .SelectMany(item => item.Property!
                .GetCustomAttributes<ValidationAttribute>(inherit: true)
                .Select(attribute =>
                    $"{item.Type.FullName}.{item.Property!.Name}: " +
                    $"{attribute.GetType().Name}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }
}
