using MapVerifier.Server.Contracts;
using MapVerifier.Server.Services;
using OsmDotRoute;
using OsmDotRoute.Itinero;

namespace MapVerifier.Server.Endpoints;

public static class LoadEndpoints
{
    public static void MapLoadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/load", (LoadRequest? request, RouterState state, RestrictedAreaService restrictions) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RouterDbPath))
            {
                return Results.BadRequest(new ErrorResponse("missing_path", "routerDbPath を指定してください。"));
            }

            try
            {
                var routerDb = ItineroRouterDbLoader.LoadFromFile(request.RouterDbPath);
                var router = new Router(routerDb, restrictions);
                state.Set(routerDb, router, request.RouterDbPath);
                return Results.Ok(BuildStats(routerDb));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse("invalid_path", ex.Message));
            }
            catch (FileNotFoundException ex)
            {
                return Results.BadRequest(new ErrorResponse("file_not_found", ex.Message));
            }
        });

        app.MapGet("/api/stats", (RouterState state) =>
        {
            var routerDb = state.RouterDb;
            return routerDb is null
                ? Results.Conflict(new ErrorResponse("not_loaded", "RouterDb が未ロードです。先に /api/load を呼んでください。"))
                : Results.Ok(BuildStats(routerDb));
        });
    }

    internal static StatsResponse BuildStats(RouterDb routerDb)
    {
        var stats = routerDb.GetStatistics();
        return new StatsResponse(
            stats.VertexCount,
            stats.EdgeCount,
            new CoordinateDto(stats.SouthWest.Latitude, stats.SouthWest.Longitude),
            new CoordinateDto(stats.NorthEast.Latitude, stats.NorthEast.Longitude));
    }
}
