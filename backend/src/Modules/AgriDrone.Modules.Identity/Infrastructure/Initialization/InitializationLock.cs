namespace AgriDrone.Modules.Identity.Infrastructure.Initialization;

public sealed class InitializationLock
{
    public const string SystemAdminBootstrapName =
        "system-admin-bootstrap";

    private InitializationLock()
    {
    }

    public string Name { get; private set; } = null!;

    public long Version { get; private set; }

    public void Acquire()
    {
        Version = checked(Version + 1);
    }
}
