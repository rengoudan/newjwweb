using Jw.Domain.Auditing;

namespace Jw.Domain.Common;

/// <summary>システム設定（キーと値）。旧版はパソコンごとの appconfig.json に保存していた出力設定など。</summary>
public class SystemSetting : AuditedEntity, IAuditable
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";

    public string AuditDisplay => Key;

    public static class Keys
    {
        /// <summary>孔径（mm）。旧版 Csv.Kongjing。</summary>
        public const string HoleDiameter = "Export.HoleDiameter";
        /// <summary>梁製造 CSV の Y 方向調整（mm）。旧版 Csv.Ytiaozheng。</summary>
        public const string CsvYAdjust = "Export.CsvYAdjust";
    }
}
