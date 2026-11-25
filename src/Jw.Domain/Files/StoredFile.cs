using Jw.Domain.Common;

namespace Jw.Domain.Files;

/// <summary>
/// サーバーに保存したファイル（取り込んだ図面など）。実体はファイル保存先に StorageKey の名前で置く。
/// 図面は階を削除しても残す（解析の追跡・再解析用）。
/// </summary>
public class StoredFile : Entity
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    /// <summary>SHA-256（16 進）。同じ図面の再取り込みの判定などに使う。</summary>
    public string Sha256 { get; set; } = "";
    /// <summary>保存先での名前（日付フォルダ/Guid.拡張子）。</summary>
    public string StorageKey { get; set; } = "";
    /// <summary>用途（"drawing" など）。</summary>
    public string Purpose { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }
}
