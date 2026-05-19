using System.Diagnostics;
using OsmDotRoute.Itinero;

namespace OsmDotRoute.Benchmarks;

/// <summary>
/// RouterDb 読み込み直後のメモリ実測（REQ-NFR-003）。
/// BenchmarkDotNet の MemoryDiagnoser は累積アロケーションを記録するが、
/// ピーク管理メモリ・プロセス Working Set は別途 GC 強制 → 計測が必要。
/// </summary>
internal static class MemoryProbe
{
    public static void RunAll()
    {
        Console.WriteLine("=== メモリプローブ (REQ-NFR-003) ===");
        var process = Process.GetCurrentProcess();
        var baselineManaged = GC.GetTotalMemory(forceFullCollection: true);
        var baselineWorking = process.WorkingSet64;
        Console.WriteLine($"開始時: ManagedHeap = {Format(baselineManaged)}, WorkingSet = {Format(baselineWorking)}");

        Console.WriteLine();
        Console.WriteLine("--- Itinero RouterDb 単独 ---");
        var sw = Stopwatch.StartNew();
        var itineroDb = BenchmarkAssets.LoadItineroRouterDb();
        sw.Stop();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var afterItineroManaged = GC.GetTotalMemory(forceFullCollection: true);
        process.Refresh();
        var afterItineroWorking = process.WorkingSet64;
        Console.WriteLine($"ロード時間: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"ManagedHeap 増加: {Format(afterItineroManaged - baselineManaged)} (絶対値 {Format(afterItineroManaged)})");
        Console.WriteLine($"WorkingSet 増加: {Format(afterItineroWorking - baselineWorking)} (絶対値 {Format(afterItineroWorking)})");

        Console.WriteLine();
        Console.WriteLine("--- OsmDotRoute (Itinero ラップ込み) ---");
        sw.Restart();
        var osmDb = ItineroRouterDbLoader.FromItineroRouterDb(itineroDb);
        sw.Stop();
        GC.KeepAlive(osmDb);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var afterOsmManaged = GC.GetTotalMemory(forceFullCollection: true);
        process.Refresh();
        var afterOsmWorking = process.WorkingSet64;
        Console.WriteLine($"ラップ追加時間: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"ManagedHeap 増加 (Itinero 比): {Format(afterOsmManaged - afterItineroManaged)} (絶対値 {Format(afterOsmManaged)})");
        Console.WriteLine($"WorkingSet 増加 (Itinero 比): {Format(afterOsmWorking - afterItineroWorking)} (絶対値 {Format(afterOsmWorking)})");

        Console.WriteLine();
        Console.WriteLine("--- 統計 ---");
        var stats = osmDb.GetStatistics();
        Console.WriteLine($"Vertices: {stats.VertexCount:N0}, Edges: {stats.EdgeCount:N0}");
        Console.WriteLine($"Bounds: ({stats.SouthWest.Latitude:F4}, {stats.SouthWest.Longitude:F4}) - ({stats.NorthEast.Latitude:F4}, {stats.NorthEast.Longitude:F4})");
    }

    private static string Format(long bytes)
    {
        if (bytes < 0) return $"-{Format(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
