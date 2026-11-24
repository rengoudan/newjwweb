namespace Jw.Domain.Auditing;

public enum AuditCategory
{
    /// <summary>业务数据的增、改、删（拦截器自动记录）。</summary>
    Data = 0,
    /// <summary>业务操作，例如导出、解析、批量处理。</summary>
    Action = 1,
    /// <summary>安全相关：登录、退出、用户和角色变更。</summary>
    Security = 2,
}

/// <summary>审计日志。只追加，不提供修改和删除。</summary>
public class AuditLog
{
    public long Id { get; set; }
    /// <summary>UTC 时间。</summary>
    public DateTime Time { get; set; }
    public AuditCategory Category { get; set; }
    /// <summary>动作，见 <see cref="AuditActions"/>。</summary>
    public string Action { get; set; } = "";
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    /// <summary>对象类型（实体类名或功能名）。</summary>
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    /// <summary>一句话说明，列表里直接显示。</summary>
    public string Summary { get; set; } = "";
    /// <summary>字段变更明细（JSON 数组，元素为 <see cref="AuditChange"/>），或操作参数。</summary>
    public string? Detail { get; set; }
    /// <summary>同一次保存或同一次请求产生的记录共享此值。</summary>
    public string? CorrelationId { get; set; }
}

public sealed record AuditChange(string Property, string? Old, string? New);

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string ChangePassword = "ChangePassword";
    public const string ResetPassword = "ResetPassword";
    public const string UserCreate = "UserCreate";
    public const string UserUpdate = "UserUpdate";
    public const string UserUnlock = "UserUnlock";
    public const string RoleCreate = "RoleCreate";
    public const string RoleUpdate = "RoleUpdate";
    public const string RoleDelete = "RoleDelete";

    /// <summary>审计页面上的显示名。</summary>
    public static string DisplayName(string action) => action switch
    {
        Create => "追加",
        Update => "変更",
        Delete => "削除",
        Login => "ログイン",
        LoginFailed => "ログイン失敗",
        Logout => "ログアウト",
        ChangePassword => "パスワード変更",
        ResetPassword => "パスワードリセット",
        UserCreate => "ユーザー追加",
        UserUpdate => "ユーザー変更",
        UserUnlock => "ロック解除",
        RoleCreate => "ロール追加",
        RoleUpdate => "ロール変更",
        RoleDelete => "ロール削除",
        _ => action,
    };
}
