using OsmDotRoute;
using Sandbox.Server.Contracts;
using Sandbox.Server.Services;

namespace Sandbox.Server.Endpoints;

public static class LoadEndpoints
{
    public static void MapLoadEndpoints(this WebApplication app)
    {
        app.MapPost("/api/load", async (LoadRequest req, SandboxState state) =>
        {
            if (string.IsNullOrWhiteSpace(req.OdrgPath))
                return Results.BadRequest(new ErrorResponse("bad_request", "odrgPath is required"));

            if (!File.Exists(req.OdrgPath))
                return Results.BadRequest(new ErrorResponse("not_found", $"File not found: {req.OdrgPath}"));

            try
            {
                var routerDb = await Task.Run(() => RouterDb.LoadFromOdrg(req.OdrgPath));
                var router = new Router(routerDb);
                state.Set(routerDb, router, req.OdrgPath);

                var stats = routerDb.GetStatistics();
                return Results.Ok(new StatsResponse(
                    stats.VertexCount,
                    stats.EdgeCount,
                    new CoordinateDto(stats.SouthWest.Latitude, stats.SouthWest.Longitude),
                    new CoordinateDto(stats.NorthEast.Latitude, stats.NorthEast.Longitude),
                    Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new ErrorResponse("load_error", ex.Message));
            }
        });
    }
}
