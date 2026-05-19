using System.Text.Json;
using MapVerifier.Server.Contracts;
using OsmDotRoute;

namespace MapVerifier.Server.Endpoints;

public static class RestrictionEndpoints
{
    public static void MapRestrictionEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/restrictions/polygon
        app.MapPost("/api/restrictions/polygon", (PolygonRestrictionRequest? req, RestrictedAreaService restrictions) =>
        {
            if (req is null || req.OuterBoundary is null || req.OuterBoundary.Length < 3)
            {
                return Results.BadRequest(new ErrorResponse("invalid_polygon", "outerBoundary は 3 頂点以上必要です。"));
            }
            var kind = (req.Kind ?? "").Trim().ToLowerInvariant();
            var polygon = new GeoPolygon(req.OuterBoundary.Select(c => new GeoCoordinate(c.Latitude, c.Longitude)).ToArray());

            Guid id;
            switch (kind)
            {
                case "block":
                    id = restrictions.AddBlockArea(polygon, req.Tag).Value;
                    break;
                case "difficulty":
                    if (string.IsNullOrWhiteSpace(req.DifficultyType))
                    {
                        return Results.BadRequest(new ErrorResponse("missing_difficulty_type", "kind=difficulty の場合は difficultyType が必要です。"));
                    }
                    id = restrictions.AddDifficultyArea(polygon, req.DifficultyType, req.Tag).Value;
                    break;
                default:
                    return Results.BadRequest(new ErrorResponse("invalid_kind", $"kind は block / difficulty のいずれか (受信: {req.Kind})"));
            }
            return Results.Ok(new RestrictionIdResponse(id));
        });

        // POST /api/restrictions/mesh
        app.MapPost("/api/restrictions/mesh", (MeshRestrictionRequest? req, RestrictedAreaService restrictions) =>
        {
            if (req is null || req.MeshCodes is null || req.MeshCodes.Length == 0)
            {
                return Results.BadRequest(new ErrorResponse("invalid_mesh", "meshCodes は 1 個以上必要です。"));
            }
            var kind = (req.Kind ?? "").Trim().ToLowerInvariant();
            var codes = req.MeshCodes.Select(v => new MeshCode(v)).ToArray();

            Guid id;
            try
            {
                switch (kind)
                {
                    case "block":
                        id = codes.Length == 1
                            ? restrictions.AddBlockArea(codes[0], req.Tag).Value
                            : restrictions.AddBlockArea(codes, req.Tag).Value;
                        break;
                    case "difficulty":
                        if (string.IsNullOrWhiteSpace(req.DifficultyType))
                        {
                            return Results.BadRequest(new ErrorResponse("missing_difficulty_type", "kind=difficulty の場合は difficultyType が必要です。"));
                        }
                        id = codes.Length == 1
                            ? restrictions.AddDifficultyArea(codes[0], req.DifficultyType, req.Tag).Value
                            : restrictions.AddDifficultyArea(codes, req.DifficultyType, req.Tag).Value;
                        break;
                    default:
                        return Results.BadRequest(new ErrorResponse("invalid_kind", $"kind は block / difficulty のいずれか (受信: {req.Kind})"));
                }
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse("invalid_argument", ex.Message));
            }
            return Results.Ok(new RestrictionIdResponse(id));
        });

        // POST /api/restrictions/gml-file
        app.MapPost("/api/restrictions/gml-file", (GmlImportRequest? req, RestrictedAreaService restrictions) =>
        {
            if (req is null || string.IsNullOrWhiteSpace(req.FilePath))
            {
                return Results.BadRequest(new ErrorResponse("missing_path", "filePath を指定してください。"));
            }
            if (!File.Exists(req.FilePath))
            {
                return Results.BadRequest(new ErrorResponse("file_not_found", $"ファイルが存在しません: {req.FilePath}"));
            }
            var kind = (req.Kind ?? "").Trim().ToLowerInvariant();

            MapBounds? bounds = null;
            if (req.UseMapBounds)
            {
                if (req.MapBoundsSouthWest is null || req.MapBoundsNorthEast is null)
                {
                    return Results.BadRequest(new ErrorResponse("missing_bounds", "useMapBounds=true の場合は mapBoundsSouthWest / mapBoundsNorthEast が必要です。"));
                }
                bounds = new MapBounds(
                    new GeoCoordinate(req.MapBoundsSouthWest.Latitude, req.MapBoundsSouthWest.Longitude),
                    new GeoCoordinate(req.MapBoundsNorthEast.Latitude, req.MapBoundsNorthEast.Longitude));
            }

            RestrictedAreaId[] ids;
            try
            {
                switch (kind)
                {
                    case "block":
                        ids = restrictions.AddBlockAreaFromGmlFile(req.FilePath, bounds, req.Tag);
                        break;
                    case "difficulty":
                        if (string.IsNullOrWhiteSpace(req.DifficultyType))
                        {
                            return Results.BadRequest(new ErrorResponse("missing_difficulty_type", "kind=difficulty の場合は difficultyType が必要です。"));
                        }
                        ids = restrictions.AddDifficultyAreaFromGmlFile(req.FilePath, req.DifficultyType, bounds, req.Tag);
                        break;
                    default:
                        return Results.BadRequest(new ErrorResponse("invalid_kind", $"kind は block / difficulty のいずれか (受信: {req.Kind})"));
                }
            }
            catch (OsmDotRoute.Gml.InvalidGmlException ex)
            {
                return Results.BadRequest(new ErrorResponse("invalid_gml", ex.Message));
            }
            catch (NotSupportedException ex)
            {
                return Results.BadRequest(new ErrorResponse("not_supported", ex.Message));
            }

            var guids = ids.Select(i => i.Value).ToArray();
            return Results.Ok(new GmlImportResponse(guids, guids.Length));
        });

        // GET /api/restrictions  — 一覧 (描画情報込み)
        app.MapGet("/api/restrictions", (RestrictedAreaService restrictions) =>
        {
            var items = restrictions.ListAll().Select(BuildItemDto).ToArray();
            return Results.Ok(new { items });
        });

        // GET /api/restrictions/geojson  — マップ描画用
        app.MapGet("/api/restrictions/geojson", (RestrictedAreaService restrictions) =>
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                w.WriteString("type", "FeatureCollection");
                w.WritePropertyName("features");
                w.WriteStartArray();
                foreach (var area in restrictions.ListAll())
                {
                    WriteAreaFeatures(w, area);
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Results.Text(System.Text.Encoding.UTF8.GetString(ms.ToArray()), "application/geo+json");
        });

        // DELETE /api/restrictions/{id}
        app.MapDelete("/api/restrictions/{id:guid}", (Guid id, RestrictedAreaService restrictions) =>
        {
            restrictions.Remove(new RestrictedAreaId(id));
            return Results.NoContent();
        });

        // DELETE /api/restrictions  (全クリア or ?tag=X)
        app.MapDelete("/api/restrictions", (string? tag, RestrictedAreaService restrictions) =>
        {
            if (!string.IsNullOrEmpty(tag))
            {
                restrictions.RemoveByTag(tag);
            }
            else
            {
                restrictions.ClearAll();
            }
            return Results.NoContent();
        });
    }

    private static object BuildItemDto(RestrictedArea area)
    {
        var (kind, difficultyType) = area switch
        {
            BlockArea => ("block", (string?)null),
            DifficultyArea d => ("difficulty", d.DifficultyType),
            _ => ("unknown", (string?)null),
        };
        var polygon = (area as BlockArea)?.Polygon ?? (area as DifficultyArea)?.Polygon;
        var meshCodes = (area as BlockArea)?.MeshCodes ?? (area as DifficultyArea)?.MeshCodes;

        return new
        {
            id = area.Id.Value,
            kind,
            difficultyType,
            shapeType = polygon is not null ? "polygon" : "mesh",
            outerBoundary = polygon?.OuterBoundary.Select(c => new CoordinateDto(c.Latitude, c.Longitude)).ToArray(),
            meshCodes = meshCodes?.Select(m => m.Value).ToArray(),
            tag = area.Tag,
        };
    }

    private static void WriteAreaFeatures(Utf8JsonWriter w, RestrictedArea area)
    {
        var (kind, difficultyType) = area switch
        {
            BlockArea => ("block", (string?)null),
            DifficultyArea d => ("difficulty", d.DifficultyType),
            _ => ("unknown", (string?)null),
        };
        var polygon = (area as BlockArea)?.Polygon ?? (area as DifficultyArea)?.Polygon;
        var meshCodes = (area as BlockArea)?.MeshCodes ?? (area as DifficultyArea)?.MeshCodes;

        if (polygon is not null)
        {
            var ring = polygon.OuterBoundary.Select(c => new CoordinateDto(c.Latitude, c.Longitude)).ToArray();
            WritePolygonFeature(w, area.Id.Value, kind, difficultyType, area.Tag, "polygon", ring, null);
        }
        else if (meshCodes is not null)
        {
            foreach (var m in meshCodes)
            {
                var b = m.ToBounds();
                var ring = new[]
                {
                    new CoordinateDto(b.SouthWest.Latitude, b.SouthWest.Longitude),
                    new CoordinateDto(b.SouthWest.Latitude, b.NorthEast.Longitude),
                    new CoordinateDto(b.NorthEast.Latitude, b.NorthEast.Longitude),
                    new CoordinateDto(b.NorthEast.Latitude, b.SouthWest.Longitude),
                };
                WritePolygonFeature(w, area.Id.Value, kind, difficultyType, area.Tag, "mesh", ring, m.Value);
            }
        }
    }

    private static void WritePolygonFeature(
        Utf8JsonWriter w,
        Guid id,
        string kind,
        string? difficultyType,
        string? tag,
        string shapeType,
        CoordinateDto[] boundary,
        long? meshCode)
    {
        w.WriteStartObject();
        w.WriteString("type", "Feature");
        w.WritePropertyName("properties");
        w.WriteStartObject();
        w.WriteString("id", id.ToString());
        w.WriteString("kind", kind);
        if (difficultyType is not null) w.WriteString("difficultyType", difficultyType);
        if (tag is not null) w.WriteString("tag", tag);
        w.WriteString("shapeType", shapeType);
        if (meshCode.HasValue) w.WriteString("meshCode", meshCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteEndObject();
        w.WritePropertyName("geometry");
        w.WriteStartObject();
        w.WriteString("type", "Polygon");
        w.WritePropertyName("coordinates");
        w.WriteStartArray();
        w.WriteStartArray();
        foreach (var c in boundary)
        {
            w.WriteStartArray();
            w.WriteNumberValue(c.Longitude);
            w.WriteNumberValue(c.Latitude);
            w.WriteEndArray();
        }
        // 閉じる
        w.WriteStartArray();
        w.WriteNumberValue(boundary[0].Longitude);
        w.WriteNumberValue(boundary[0].Latitude);
        w.WriteEndArray();
        w.WriteEndArray();
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
    }
}
