namespace Jw.Application.Files;

/// <summary>ファイルの保存先（実装は Infrastructure：サーバーのフォルダ）。</summary>
public interface IFileStore
{
    /// <summary>保存して保存キー（相対パス）を返す。</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    /// <summary>保存キーに対応する実ファイルのフルパス（解析器が直接読むため）。</summary>
    string GetFullPath(string storageKey);

    Stream OpenRead(string storageKey);
}
