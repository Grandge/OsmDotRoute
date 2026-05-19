using MapVerifier.Server.Contracts;

namespace MapVerifier.Server.Endpoints;

public static class BrowseEndpoints
{
    /// <summary>
    /// ローカルファイルシステム参照用エンドポイント。Web 内モーダルで使う自前ファイルブラウザを駆動する。
    /// OS ネイティブダイアログ (POST /api/files/pick) はホスト環境次第で表示されないため、
    /// この HTTP ベース方式に置換した (親プロジェクト UserSettingsDialog 方式に倣う)。
    /// 個人ローカル実行前提のため認可は無い。
    /// </summary>
    public static void MapBrowseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/browse", (string? path, string? pattern) =>
        {
            string currentPath;
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                else
                {
                    currentPath = Path.GetFullPath(path);
                }

                if (!Directory.Exists(currentPath))
                {
                    return Results.BadRequest(new ErrorResponse("not_found", $"パスが見つかりません: {currentPath}"));
                }

                var parent = Directory.GetParent(currentPath)?.FullName;

                var subDirs = SafeEnumerate(() => Directory.EnumerateDirectories(currentPath))
                    .Where(p => !IsHiddenOrSystem(p))
                    .Select(p => new { name = Path.GetFileName(p) })
                    .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var patterns = string.IsNullOrWhiteSpace(pattern)
                    ? Array.Empty<string>()
                    : pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var filePaths = patterns.Length == 0
                    ? Array.Empty<string>()
                    : patterns
                        .SelectMany(pat => SafeEnumerate(() => Directory.EnumerateFiles(currentPath, pat)))
                        .Where(p => !IsHiddenOrSystem(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                var files = filePaths
                    .Select(p =>
                    {
                        long size = 0;
                        try { size = new FileInfo(p).Length; } catch { /* swallow */ }
                        return new { name = Path.GetFileName(p), size };
                    })
                    .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => d.Name)
                    .ToArray();

                return Results.Ok(new
                {
                    currentPath,
                    parentPath = parent,
                    directories = subDirs,
                    files,
                    drives,
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.BadRequest(new ErrorResponse("access_denied", ex.Message));
            }
            catch (IOException ex)
            {
                return Results.BadRequest(new ErrorResponse("io_error", ex.Message));
            }
        });
    }

    private static IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> source)
    {
        try
        {
            return source().ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System);
        }
        catch
        {
            return false;
        }
    }
}
