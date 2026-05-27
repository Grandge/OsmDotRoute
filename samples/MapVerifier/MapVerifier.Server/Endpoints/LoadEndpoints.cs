using MapVerifier.Server.Contracts;
using MapVerifier.Server.Services;
using OsmDotRoute;

namespace MapVerifier.Server.Endpoints;

/// <summary>
/// .odrg ロード経路 (Phase 3 ステップ 3C.4 で Itinero RouterDb から .odrg に切替)。
/// DTO の <c>routerDbPath</c> フィールド名は後方互換のため維持しているが、実態は .odrg ファイルパス。
/// </summary>
public static class LoadEndpoints
{
    public static void MapLoadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/load", (LoadRequest? request, RouterState state, RestrictedAreaService restrictions) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RouterDbPath))
            {
                return Results.BadRequest(new ErrorResponse("missing_path", "routerDbPath (.odrg ファイルパス) を指定してください。"));
            }

            try
            {
                var routerDb = RouterDb.LoadFromOdrg(request.RouterDbPath);
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
            catch (Exception ex) when (ex.GetType().Name == "OdrgFormatException")
            {
                // OdrgFormatException は OsmDotRoute コアで internal 定義のため型直接 catch 不可
                // (Phase 4+ で public 化検討、現状はクラス名照合で対応)
                return Results.BadRequest(new ErrorResponse("invalid_format", ex.Message));
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
