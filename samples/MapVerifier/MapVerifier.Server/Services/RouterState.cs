using OsmDotRoute;

namespace MapVerifier.Server.Services;

/// <summary>
/// プロセス全体で共有する RouterDb / Router のホルダー。/api/load 時に差し替えられる。
/// </summary>
public sealed class RouterState
{
    private readonly object _lock = new();
    private RouterDb? _routerDb;
    private Router? _router;
    private string? _loadedPath;

    public bool IsLoaded => _routerDb is not null;

    public string? LoadedPath
    {
        get { lock (_lock) { return _loadedPath; } }
    }

    public RouterDb? RouterDb
    {
        get { lock (_lock) { return _routerDb; } }
    }

    public Router? Router
    {
        get { lock (_lock) { return _router; } }
    }

    public void Set(RouterDb routerDb, Router router, string path)
    {
        ArgumentNullException.ThrowIfNull(routerDb);
        ArgumentNullException.ThrowIfNull(router);
        lock (_lock)
        {
            _routerDb = routerDb;
            _router = router;
            _loadedPath = path;
        }
    }
}
