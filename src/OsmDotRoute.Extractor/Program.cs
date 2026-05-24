using System;
using System.CommandLine;
using OsmDotRoute.Extractor.Cli;

var rootCommand = new RootCommand(
    "OsmDotRoute Extractor — OSM PBF から .odrg バイナリグラフを抽出する CLI。" +
    " 詳細: Documents/phase2_graph_format_spec.md v0.2 を参照。")
{
    ExtractCommand.Build(Run),
};

return rootCommand.Parse(args).Invoke();

static int Run(ExtractOptions options)
{
    // サブステップ 3.1: 受信パラメータをエコー表示するのみ。
    // 抽出パイプライン本体はサブステップ 3.2 以降で実装する。
    Console.WriteLine($"input    : {options.Input.FullName}");
    Console.WriteLine($"output   : {options.Output.FullName}");
    Console.WriteLine($"bbox     : {options.Bbox}");
    Console.WriteLine($"profiles : {string.Join(",", options.Profiles)}");
    Console.WriteLine();
    Console.WriteLine("(extraction pipeline not yet implemented — Phase 2 substep 3.1 only validates CLI parsing)");
    return 0;
}
