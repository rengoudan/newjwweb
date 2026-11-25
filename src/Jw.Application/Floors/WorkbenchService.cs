using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

public enum TodoKind
{
    /// <summary>要確認（梁・柱なし、穴なし梁あり）。</summary>
    NeedsCheck,
    /// <summary>未取り込みの階がある工事。</summary>
    ImportPending,
    /// <summary>梁製造 CSV をまだ出力していない階。</summary>
    CsvPending,
}

public sealed record TodoItem(TodoKind Kind, string Title, string Detail, string Href, DateTime At);

public sealed record WorkbenchSummary(
    int Projects, int ProjectsInProgress, int Floors, int FloorsNeedCheck, int FloorsCsvPending, int Budgets,
    IReadOnlyList<TodoItem> Todos);

/// <summary>ワークベンチ（ホーム）：件数と「対応が必要なもの」（旧版ワークベンチと同じ 3 種類）。</summary>
public sealed class WorkbenchService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, FloorService floors)
{
    public async Task<WorkbenchSummary> GetAsync(CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Workbench.View);
        var canProjects = currentUser.HasPermission(Permissions.Projects.View);
        await using var db = await dbFactory.CreateAsync(ct);

        var projects = await db.Projects.AsNoTracking().CountAsync(ct);
        var pending = canProjects
            ? await db.Projects.AsNoTracking().Where(p => p.ParsedQuantity < p.FloorQuantity).OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.Id, p.Name, p.ParsedQuantity, p.FloorQuantity, p.CreatedAt }).ToListAsync(ct)
            : [];
        var floorCount = await db.Floors.AsNoTracking().CountAsync(f => !f.Project.IsDeleted, ct);
        var csvPending = canProjects
            ? await db.Floors.AsNoTracking().Where(f => f.ExportCount == 0 && !f.Project.IsDeleted).OrderByDescending(f => f.CreatedAt)
                .Select(f => new { f.Id, f.Name, Project = f.Project.Name, f.CreatedAt }).ToListAsync(ct)
            : [];
        var budgets = await db.Budgets.AsNoTracking().CountAsync(b => !b.Project.IsDeleted, ct);
        var check = canProjects ? await floors.NeedsCheckAsync(50, ct) : [];

        var todos = new List<TodoItem>();
        todos.AddRange(check.Select(c => new TodoItem(TodoKind.NeedsCheck, $"{c.ProjectName} {c.Floor.Name}", c.Floor.Check.Reason,
            $"/floors/{c.Floor.Id}", c.Floor.CreatedAt)));
        todos.AddRange(pending.Select(p => new TodoItem(TodoKind.ImportPending, p.Name,
            $"取り込み済み {p.ParsedQuantity} / {p.FloorQuantity} 階", $"/projects/{p.Id}", p.CreatedAt)));
        todos.AddRange(csvPending.Select(f => new TodoItem(TodoKind.CsvPending, $"{f.Project} {f.Name}",
            "梁製造 CSV をまだ出力していません", $"/floors/{f.Id}", f.CreatedAt)));

        return new WorkbenchSummary(projects, pending.Count, floorCount, check.Count, csvPending.Count, budgets,
            todos.OrderBy(t => t.Kind).ThenByDescending(t => t.At).Take(30).ToList());
    }
}
