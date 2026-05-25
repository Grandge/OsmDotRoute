using OsmDotRoute.Extractor.Pipeline;

namespace MapVerifier.Server.Services;

/// <summary>
/// プロセス全体で共有する <see cref="OdrgReadResult"/> ホルダー。
/// <c>/api/load-odrg</c> 時に差し替えられる。<see cref="RouterState"/> と並列に動作する。
/// </summary>
public sealed class OdrgState
{
    private readonly object _lock = new();
    private OdrgReadResult? _result;
    private string? _loadedPath;

    public bool IsLoaded
    {
        get { lock (_lock) { return _result is not null; } }
    }

    public string? LoadedPath
    {
        get { lock (_lock) { return _loadedPath; } }
    }

    internal OdrgReadResult? Result
    {
        get { lock (_lock) { return _result; } }
    }

    internal void Set(OdrgReadResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(path);
        lock (_lock)
        {
            _result = result;
            _loadedPath = path;
        }
    }
}
