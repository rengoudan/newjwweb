using Jw.Application.Files;
using Microsoft.Extensions.Options;

namespace Jw.Infrastructure.Files;

public sealed class StorageOptions
{
    /// <summary>ファイル保存先のフォルダ（相対パスはアプリのフォルダ基準）。</summary>
    public string Root { get; set; } = "App_Data/files";
}

/// <summary>サーバーのフォルダに保存する。保存キーは「年/月/Guid.拡張子」。</summary>
internal sealed class LocalFileStore(IOptions<StorageOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.Root, AppContext.BaseDirectory);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = GetFullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public string GetFullPath(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        // 保存キーが保存先フォルダの外を指さないことを確認する
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("不正な保存キーです。");
        return path;
    }

    public Stream OpenRead(string storageKey) => File.OpenRead(GetFullPath(storageKey));
}
