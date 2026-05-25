using MapVerifier.Server.Contracts;
using MapVerifier.Server.Services;
using OsmDotRoute;

namespace MapVerifier.Server.Endpoints;

public static class SnapAndRouteEndpoints
{
    public static void MapSnapAndRouteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/snap", (SnapRequest? request, RouterState state) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new ErrorResponse("missing_body", "リクエストボディが必要です。"));
            }
            var router = state.Router;
            if (router is null)
            {
                return Results.Conflict(new ErrorResponse("not_loaded", "RouterDb が未ロードです。"));
            }
            var profile = ResolveProfile(request.Profile);
            if (profile is null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_profile", $"profile が不正です: {request.Profile}"));
            }
            var search = request.SearchDistanceM ?? 500f;
            var snapped = router.SnapToRoad(profile, new GeoCoordinate(request.Lat, request.Lon), search);
            return Results.Ok(new SnapResponse(snapped is null ? null : new CoordinateDto(snapped.Value.Latitude, snapped.Value.Longitude)));
        });

        app.MapPost("/api/route", (RouteRequest? request, RouterState state) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new ErrorResponse("missing_body", "リクエストボディが必要です。"));
            }
            // graphSource 切替: routerdb (default) | odrg (Phase 3 で対応予定)
            var graphSource = (request.GraphSource ?? "routerdb").Trim().ToLowerInvariant();
            if (graphSource == "odrg")
            {
                return Results.Json(
                    new ErrorResponse(
                        "not_implemented",
                        ".odrg からの経路計算は Phase 3 で対応予定です。現在は RouterDb を選択してください。"),
                    statusCode: StatusCodes.Status501NotImplemented);
            }
            if (graphSource != "routerdb")
            {
                return Results.BadRequest(new ErrorResponse(
                    "invalid_graph_source",
                    $"graphSource が不正です: {request.GraphSource}。'routerdb' または 'odrg' を指定してください。"));
            }

            var router = state.Router;
            if (router is null)
            {
                return Results.Conflict(new ErrorResponse("not_loaded", "RouterDb が未ロードです。"));
            }
            var profile = ResolveProfile(request.Profile);
            if (profile is null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_profile", $"profile が不正です: {request.Profile}"));
            }
            var route = router.Calculate(
                profile,
                new GeoCoordinate(request.FromLat, request.FromLon),
                new GeoCoordinate(request.ToLat, request.ToLon));
            if (route is null)
            {
                return Results.Ok(new RouteResponse(false, 0, 0, null));
            }
            return Results.Ok(new RouteResponse(
                true,
                route.TotalDistanceM,
                route.TotalDurationSec,
                GeoJsonConverter.ToLineString(route)));
        });
    }

    private static VehicleProfile? ResolveProfile(string? name)
    {
        return (name ?? "car").Trim().ToLowerInvariant() switch
        {
            "car" or "" => VehicleProfile.Car,
            "pedestrian" => VehicleProfile.Pedestrian,
            _ => null,
        };
    }
}
