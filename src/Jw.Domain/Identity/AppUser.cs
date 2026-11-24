using Microsoft.AspNetCore.Identity;

namespace Jw.Domain.Identity;

/// <summary>系统用户。用户不物理删除，只停用，保证审计记录可追溯。</summary>
public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>首次登录或被管理员重置密码后，必须先改密码。</summary>
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();

    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>系统预置角色不能删除；管理员角色的权限固定为全部。</summary>
    public bool IsSystem { get; set; }
}

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
}
