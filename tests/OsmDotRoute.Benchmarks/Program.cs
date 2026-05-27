using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using OsmDotRoute.Benchmarks;
using OsmDotRoute.Benchmarks.Generators;

// 起動時引数:
//   --generate-data   route-pairs.json と restrictions-*.geojson を生成して終了
//   その他は BenchmarkDotNet にそのまま渡す（--filter, --list, --job 等）
if (args.Length > 0 && args[0] == "--generate-data")
{
    var generated = TestDataInitializer.GenerateAll();
    Console.WriteLine($"TestData 生成完了: {generated.RoutePairsPath}, {generated.MixedGeoJsonPath}, {generated.BlockGeoJsonPath}");
    return;
}

var config = ManualConfig.CreateMinimumViable()
    .AddDiagnoser(MemoryDiagnoser.Default)
    .WithOptions(ConfigOptions.JoinSummary | ConfigOptions.DisableLogFile);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

internal sealed partial class Program;
