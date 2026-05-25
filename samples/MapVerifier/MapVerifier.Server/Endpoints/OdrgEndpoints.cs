using MapVerifier.Server.Contracts;
using MapVerifier.Server.Services;
using OsmDotRoute.Extractor.Pipeline;

namespace MapVerifier.Server.Endpoints;

/// <summary>
/// Phase 2 ステップ 5.4。<c>.odrg</c> を読み込んで地図にオーバーレイ表示するためのエンドポイント。
/// 既存 <c>/api/load</c> / <c>/api/road-network</c> (Phase 1 RouterDb) と並列に動作する。
/// </summary>
public static class OdrgEndpoints
{
    public static void MapOdrgEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/load-odrg", (LoadOdrgRequest? request, OdrgState state) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.OdrgPath))
                return Results.BadRequest(new ErrorResponse("missing_path", "odrgPath を指定してください。"));

            try
            {
                OdrgReadResult result = OdrgReader.Read(request.OdrgPath);
                state.Set(result, request.OdrgPath);
                return Results.Ok(BuildStats(result));
            }
            catch (FileNotFoundException ex)
            {
                return Results.BadRequest(new ErrorResponse("file_not_found", ex.Message));
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new ErrorResponse("invalid_odrg", ex.Message));
            }
        });

        app.MapGet("/api/stats-odrg", (OdrgState state) =>
        {
            var result = state.Result;
            return result is null
                ? Results.Conflict(new ErrorResponse("not_loaded", ".odrg が未ロードです。先に /api/load-odrg を呼んでください。"))
                : Results.Ok(BuildStats(result));
        });

        app.MapGet("/api/road-network-odrg", (OdrgState state) =>
        {
            var result = state.Result;
            if (result is null)
                return Results.Conflict(new ErrorResponse("not_loaded", ".odrg が未ロードです。"));

            string json = OdrgGeoJsonWriter.WriteRoadNetwork(result);
            return Results.Text(json, "application/geo+json");
        });
    }

    internal static OdrgStatsResponse BuildStats(OdrgReadResult result)
    {
        var bbox = result.Header.Bbox;
        return new OdrgStatsResponse(
            VertexCount: result.Vertices.Length,
            EdgeCount: result.Edges.Length,
            SouthWest: new CoordinateDto(bbox.MinLat, bbox.MinLon),
            NorthEast: new CoordinateDto(bbox.MaxLat, bbox.MaxLon),
            ProfileNames: result.ProfileTable.ProfileNames);
    }
}
