namespace OsmDotRoute.Gml;

/// <summary>
/// GML 入力の形式不正・必須要素欠落・xlink 参照解決失敗等を表す例外（REQ-RST-020〜025）。
/// </summary>
public sealed class InvalidGmlException : Exception
{
    /// <summary>新規例外を作成する。</summary>
    public InvalidGmlException(string message) : base(message)
    {
    }

    /// <summary>内部例外つきで新規例外を作成する。</summary>
    public InvalidGmlException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
