using Jw.Domain.Identity;

namespace Jw.Domain.Authorization;

public sealed record PermissionDefinition(string Name, string DisplayName);
public sealed record PermissionGroup(string Name, string DisplayName, IReadOnlyList<PermissionDefinition> Permissions);

/// <summary>
/// 权限点，粒度为“模块.操作”。权限挂在角色上（RoleClaim，类型为 <see cref="ClaimType"/>），
/// 页面和服务端都按权限名校验。后续阶段的模块在这里预先列出。
/// </summary>
public static class Permissions
{
    public const string ClaimType = "perm";

    public static class Workbench { public const string View = "Workbench.View"; }

    public static class Projects
    {
        public const string View = "Projects.View";
        public const string Edit = "Projects.Edit";
        public const string Delete = "Projects.Delete";
        public const string Import = "Projects.Import";
        public const string Export = "Projects.Export";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string Edit = "Customers.Edit";
        public const string Delete = "Customers.Delete";
    }

    public static class Budget
    {
        public const string View = "Budget.View";
        public const string Edit = "Budget.Edit";
    }

    public static class BaseData
    {
        public const string View = "BaseData.View";
        public const string Edit = "BaseData.Edit";
    }

    public static class Admin
    {
        public const string Users = "Admin.Users";
        public const string Roles = "Admin.Roles";
        public const string Audit = "Admin.Audit";
        public const string Settings = "Admin.Settings";
    }

    public static readonly IReadOnlyList<PermissionGroup> Groups =
    [
        new("Workbench", "ワークベンチ", [new(Workbench.View, "表示")]),
        new("Projects", "工事", [
            new(Projects.View, "表示"), new(Projects.Edit, "編集"), new(Projects.Delete, "削除"),
            new(Projects.Import, "図面取込・解析"), new(Projects.Export, "出力")]),
        new("Customers", "顧客", [new(Customers.View, "表示"), new(Customers.Edit, "編集"), new(Customers.Delete, "削除")]),
        new("Budget", "予算", [new(Budget.View, "表示"), new(Budget.Edit, "編集")]),
        new("BaseData", "基礎データ", [new(BaseData.View, "表示"), new(BaseData.Edit, "編集")]),
        new("Admin", "システム管理", [
            new(Admin.Users, "ユーザー管理"), new(Admin.Roles, "ロール管理"),
            new(Admin.Audit, "操作ログ閲覧"), new(Admin.Settings, "システム設定")]),
    ];

    public static IEnumerable<string> All => Groups.SelectMany(g => g.Permissions).Select(p => p.Name);

    public static bool IsDefined(string name) => All.Contains(name);

    public static string DisplayNameOf(string name)
    {
        foreach (var g in Groups)
            foreach (var p in g.Permissions)
                if (p.Name == name) return $"{g.DisplayName}：{p.DisplayName}";
        return name;
    }

    /// <summary>预置角色的默认权限。</summary>
    public static IEnumerable<string> DefaultsFor(string role) => role switch
    {
        SystemRoles.Admin => All,
        SystemRoles.Operator => All.Where(p => !p.StartsWith("Admin.", StringComparison.Ordinal)),
        SystemRoles.Viewer => All.Where(p => p.EndsWith(".View", StringComparison.Ordinal)),
        _ => [],
    };
}
