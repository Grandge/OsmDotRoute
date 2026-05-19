using System.Reflection;
using MapVerifier.Server.Contracts;

namespace MapVerifier.Server.Endpoints;

public static class VersionEndpoints
{
    public static void MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var assembly = typeof(VersionEndpoints).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        var version = TrimMetadata(info);

        app.MapGet("/api/version", () => new VersionResponse("MapVerifier.Server", version));
    }

    private static string TrimMetadata(string informationalVersion)
    {
        var plus = informationalVersion.IndexOf('+');
        return plus >= 0 ? informationalVersion[..plus] : informationalVersion;
    }
}
