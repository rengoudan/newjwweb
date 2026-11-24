namespace Jw.Web.Shared;

/// <summary>数据库存 UTC，页面按配置的时区（默认日本时间）显示。</summary>
public static class TimeDisplay
{
    private static TimeZoneInfo _zone = TimeZoneInfo.Local;

    public static void Configure(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return;
        try { _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { /* 用服务器本地时区 */ }
    }

    public static TimeZoneInfo Zone => _zone;

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zone);

    public static string Format(DateTime? utc) => utc is null ? "" : ToLocal(utc.Value).ToString("yyyy/MM/dd HH:mm");

    public static string FormatSeconds(DateTime? utc) => utc is null ? "" : ToLocal(utc.Value).ToString("yyyy/MM/dd HH:mm:ss");
}
