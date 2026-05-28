namespace Sandbox.Server.Contracts;

public sealed record VersionResponse(string Name, string Version);

public sealed record CoordinateDto(double Latitude, double Longitude);

public sealed record StatsResponse(
    int VertexCount,
    int EdgeCount,
    CoordinateDto SouthWest,
    CoordinateDto NorthEast,
    string[] ProfileNames);

public sealed record ErrorResponse(string Error, string Message);

public sealed record RegionResponse(string Key, string DisplayName, string Description);

public sealed record DownloadRequest(string Region);

public sealed record CachedPbfInfo(string RegionKey, string DisplayName, long SizeBytes, DateTime LastModifiedUtc);

public sealed record CacheStatusResponse(CachedPbfInfo[] Items);

public sealed record CacheDirRequest(string Path);

public sealed record CacheDirResponse(string Path);

public sealed record ExtractRequest(string PbfPath, double[]? Bbox, string[]? Profiles);

public sealed record LoadRequest(string? OdrgPath);
