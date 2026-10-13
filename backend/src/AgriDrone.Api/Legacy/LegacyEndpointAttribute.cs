namespace AgriDrone.Api.Legacy;

/// <summary>
/// Marks an HTTP entry point retained only for compatibility during the
/// Be-Plan migration. The legacy gate blocks it by default without removing
/// its route or implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LegacyEndpointAttribute(
    string routeName,
    string replacement) : Attribute
{
    public string RouteName { get; } =
        string.IsNullOrWhiteSpace(routeName)
            ? throw new ArgumentException(
                "A stable legacy route name is required.",
                nameof(routeName))
            : routeName.Trim();

    public string Replacement { get; } =
        string.IsNullOrWhiteSpace(replacement)
            ? throw new ArgumentException(
                "A replacement description is required.",
                nameof(replacement))
            : replacement.Trim();
}
