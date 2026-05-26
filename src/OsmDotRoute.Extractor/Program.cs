using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using OsmDotRoute;
using OsmDotRoute.Extractor.Cli;
using OsmDotRoute.Extractor.Pipeline;

var rootCommand = new RootCommand(
    "OsmDotRoute Extractor — OSM PBF から .odrg バイナリグラフを抽出する CLI。" +
    " 詳細: Documents/phase2_graph_format_spec.md v0.2 を参照。")
{
    ExtractCommand.Build(Run),
};

return rootCommand.Parse(args).Invoke();

static int Run(ExtractOptions options)
{
    Console.WriteLine($"input    : {options.Input.FullName}");
    Console.WriteLine($"output   : {options.Output.FullName}");
    Console.WriteLine($"bbox     : {options.Bbox}");
    Console.WriteLine($"profiles : {string.Join(",", options.Profiles)}");
    Console.WriteLine();

    if (!options.Input.Exists)
    {
        Console.Error.WriteLine($"入力 PBF が見つかりません: {options.Input.FullName}");
        return 2;
    }

    VehicleProfile[] profiles;
    try
    {
        profiles = options.Profiles.Select(ResolveProfile).ToArray();
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 2;
    }

    var bbox = new Aabb(
        MinLon: options.Bbox.MinLon,
        MinLat: options.Bbox.MinLat,
        MaxLon: options.Bbox.MaxLon,
        MaxLat: options.Bbox.MaxLat);

    var pipelineOpts = new ExtractPipelineOptions(
        InputPbf: options.Input.FullName,
        Bbox: bbox,
        Profiles: profiles);

    var sw = Stopwatch.StartNew();
    Console.WriteLine("抽出開始...");
    ExtractPipelineResult result;
    try
    {
        result = ExtractPipeline.Run(pipelineOpts);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"抽出失敗: {ex.Message}");
        return 3;
    }
    sw.Stop();

    Console.WriteLine($"抽出完了: 頂点 {result.Vertices.Length:N0} 件 / エッジ {result.Edges.Length:N0} 件 ({sw.Elapsed.TotalSeconds:F1} 秒)");

    // Metadata JSON 構築
    string metadataJson = JsonSerializer.Serialize(new
    {
        createdAt = DateTimeOffset.UtcNow.ToString("o"),
        createdBy = "OsmDotRoute.Extractor",
        sourcePbf = options.Input.Name,
        profiles = options.Profiles.ToArray(),
        bbox = new
        {
            minLon = bbox.MinLon,
            minLat = bbox.MinLat,
            maxLon = bbox.MaxLon,
            maxLat = bbox.MaxLat,
        },
        vertexCount = result.Vertices.Length,
        edgeCount = result.Edges.Length,
        rtreeBranchingFactor = result.RTree.BranchingFactor,
    });

    var writeInput = new OdrgWriteInput(
        Vertices: result.Vertices,
        Edges: result.Edges,
        EdgeAabbs: result.EdgeAabbs,
        EdgeFlags: result.EdgeFlags,
        RTree: result.RTree,
        ProfileTable: result.ProfileTable,
        NodeCoordLookup: result.NodeCoordLookup,
        Bbox: result.FileBbox,
        MetadataJson: metadataJson);

    Console.WriteLine("書出開始...");
    sw.Restart();
    using (var fs = options.Output.Open(FileMode.Create, FileAccess.Write, FileShare.None))
    {
        OdrgWriter.Write(fs, writeInput);
    }
    sw.Stop();
    long fileSize = new FileInfo(options.Output.FullName).Length;
    Console.WriteLine($"書出完了: {fileSize:N0} byte ({sw.Elapsed.TotalSeconds:F1} 秒)");
    Console.WriteLine($"出力ファイル: {options.Output.FullName}");

    return 0;
}

static VehicleProfile ResolveProfile(string name) =>
    name.ToLowerInvariant() switch
    {
        "car" => VehicleProfile.Car,
        "pedestrian" => VehicleProfile.Pedestrian,
        "bicycle" => VehicleProfile.Bicycle,
        "truck" => VehicleProfile.Truck,
        _ => throw new ArgumentException($"未対応プロファイル: '{name}'。'car' / 'pedestrian' / 'bicycle' / 'truck' のみ対応"),
    };
