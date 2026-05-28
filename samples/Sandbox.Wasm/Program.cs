using System.Runtime.InteropServices.JavaScript;
using OsmDotRoute;

Console.WriteLine("OsmDotRoute Sandbox.Wasm runtime started.");

/// <summary>
/// JS ⇔ C# 相互運用ブリッジ（Phase 3 ステップ 3J.1 雛形）。
/// 3J.3 以降で load / route / restriction などの JSExport を追加する。
/// </summary>
public partial class Interop
{
    /// <summary>
    /// 動作確認用。コアアセンブリ（OsmDotRoute）が WASM 上でロードできることを実証する。
    /// </summary>
    [JSExport]
    internal static string Version()
    {
        var core = typeof(Router).Assembly.GetName();
        return $"Sandbox.Wasm OK — core {core.Name} v{core.Version}";
    }
}
