using OsmDotRoute;

namespace Sandbox.Server.Services;

public sealed class SandboxState
{
    private readonly object _lock = new();
    private RouterDb? _routerDb;
    private Router? _router;
    private string? _loadedOdrgPath;

    public bool IsLoaded
    {
        get { lock (_lock) { return _routerDb is not null; } }
    }

    public string? LoadedOdrgPath
    {
        get { lock (_lock) { return _loadedOdrgPath; } }
    }

    public RouterDb? RouterDb
    {
        get { lock (_lock) { return _routerDb; } }
    }

    public Router? Router
    {
        get { lock (_lock) { return _router; } }
    }

    public void Set(RouterDb routerDb, Router router, string odrgPath)
    {
        ArgumentNullException.ThrowIfNull(routerDb);
        ArgumentNullException.ThrowIfNull(router);
        lock (_lock)
        {
            _routerDb = routerDb;
            _router = router;
            _loadedOdrgPath = odrgPath;
        }
    }
}
