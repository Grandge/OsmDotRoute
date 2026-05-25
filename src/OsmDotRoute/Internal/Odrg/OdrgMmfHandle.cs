using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;

namespace OsmDotRoute.Internal.Odrg;

/// <summary>
/// `.odrg` ファイルを <see cref="MemoryMappedFile"/> + <c>byte*</c> 直アクセスで保持し、
/// セクション本体を <see cref="ReadOnlySpan{T}"/> として zero-copy で公開するハンドル（Phase 3 ステップ 3A.2）。
/// </summary>
/// <remarks>
/// <para>
/// 利用契約: 必ず <see cref="Dispose"/> を呼ぶこと。<see cref="GetSpan{T}"/> で取得した
/// <see cref="ReadOnlySpan{T}"/> のライフタイムは本ハンドル <see cref="Dispose"/> 呼出までと
/// 一致する（<c>ReadOnlySpan&lt;T&gt;</c> 自体が <c>ref struct</c> であり、
/// コンパイラがフィールド保持・キャプチャ越えのライフタイム逸脱を弾く）。
/// </para>
/// <para>
/// ファイナライザは持たない。MMF / View / SafeBuffer はすべて <c>SafeHandle</c> 派生型で
/// CriticalFinalizer 経由の自動解放が効くため、本クラスが <see cref="Dispose"/> 漏れしても
/// OS リソース（マップ・ファイルハンドル）は最終的に GC で回収される（ユーザー判断 #21 (b)）。
/// </para>
/// <para>
/// パフォーマンス設計: <c>SafeBuffer.AcquirePointer</c> / <c>SafeBuffer.ReleasePointer</c>
/// は本ハンドル構築時・破棄時の 1 ペアのみ。各 <see cref="GetSpan{T}"/> 呼出では
/// <c>byte*</c> + オフセット計算のみで Span を組み立て、参照カウント操作は発生しない。
/// </para>
/// </remarks>
internal sealed unsafe class OdrgMmfHandle : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly SafeMemoryMappedViewHandle _viewHandle;
    private readonly byte* _basePtr;
    private readonly long _viewLength;
    private bool _disposed;

    /// <summary>
    /// 指定パスの `.odrg` を読み取り専用で MMF マップし、ハンドルを構築する。
    /// </summary>
    public static OdrgMmfHandle Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var mmf = MemoryMappedFile.CreateFromFile(
            path,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read);
        try
        {
            var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return new OdrgMmfHandle(mmf, accessor);
        }
        catch
        {
            mmf.Dispose();
            throw;
        }
    }

    private OdrgMmfHandle(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor)
    {
        _mmf = mmf;
        _accessor = accessor;
        _viewHandle = accessor.SafeMemoryMappedViewHandle;
        _viewLength = checked((long)_viewHandle.ByteLength);

        byte* ptr = null;
        _viewHandle.AcquirePointer(ref ptr);
        // CreateViewAccessor は align 都合でビュー先頭が AcquirePointer 取得ポインタから
        // PointerOffset ずれる場合がある。実ファイル先頭はこの分を足す必要がある。
        _basePtr = ptr + _accessor.PointerOffset;
    }

    /// <summary>マップ済ビューのバイト長（ファイル全長相当）。</summary>
    public long ViewLength
    {
        get
        {
            ThrowIfDisposed();
            return _viewLength;
        }
    }

    /// <summary>
    /// <see cref="OdrgSectionDirectory.Read(SafeMemoryMappedViewHandle, long)"/> に渡せる
    /// MMF ビューハンドルを公開する。
    /// </summary>
    public SafeMemoryMappedViewHandle ViewHandle
    {
        get
        {
            ThrowIfDisposed();
            return _viewHandle;
        }
    }

    /// <summary>
    /// ビューの <paramref name="byteOffset"/> から <paramref name="elementCount"/> 個の
    /// <typeparamref name="T"/> 要素を <see cref="ReadOnlySpan{T}"/> として zero-copy で取得する。
    /// </summary>
    /// <typeparam name="T">アンマネージド要素型（ファイル形式と完全一致するレイアウト）。</typeparam>
    /// <param name="byteOffset">ビュー先頭からのバイトオフセット（非負）。</param>
    /// <param name="elementCount">要素数（非負）。</param>
    public ReadOnlySpan<T> GetSpan<T>(long byteOffset, int elementCount) where T : unmanaged
    {
        ThrowIfDisposed();
        if (byteOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset), byteOffset, "byteOffset must be non-negative.");
        }
        if (elementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "elementCount must be non-negative.");
        }
        long byteLength = (long)elementCount * sizeof(T);
        if (byteOffset + byteLength > _viewLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteOffset),
                $"Span extends past view end: offset={byteOffset} bytes={byteLength} viewLen={_viewLength}.");
        }
        return new ReadOnlySpan<T>(_basePtr + byteOffset, elementCount);
    }

    /// <summary>
    /// ビューの <paramref name="byteOffset"/> から <paramref name="byteLength"/> バイトを
    /// <see cref="ReadOnlySpan{Byte}"/> として zero-copy で取得する（METADATA / TURN_RESTRICTION セクション向け）。
    /// </summary>
    public ReadOnlySpan<byte> GetRawSpan(long byteOffset, int byteLength)
    {
        ThrowIfDisposed();
        if (byteOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset), byteOffset, "byteOffset must be non-negative.");
        }
        if (byteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "byteLength must be non-negative.");
        }
        if (byteOffset + byteLength > _viewLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteOffset),
                $"Span extends past view end: offset={byteOffset} bytes={byteLength} viewLen={_viewLength}.");
        }
        return new ReadOnlySpan<byte>(_basePtr + byteOffset, byteLength);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            _viewHandle.ReleasePointer();
        }
        finally
        {
            _accessor.Dispose();
            _mmf.Dispose();
        }
    }
}
