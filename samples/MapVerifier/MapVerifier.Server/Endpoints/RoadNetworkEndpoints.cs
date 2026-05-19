using MapVerifier.Server.Contracts;
using MapVerifier.Server.Services;

namespace MapVerifier.Server.Endpoints;

public static class RoadNetworkEndpoints
{
    public static void MapRoadNetworkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/road-network", (RouterState state) =>
        {
            var router = state.Router;
            if (router is null)
            {
                return Results.Conflict(new ErrorResponse("not_loaded", "RouterDb が未ロードです。"));
            }
            var geoJson = router.GetRoadNetworkGeoJson().Json;
            return Results.Text(geoJson, "application/geo+json");
        });
    }
}
