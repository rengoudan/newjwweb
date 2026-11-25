using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Files;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Jw.Web.Auth;

public static class FileEndpoints
{
    /// <summary>保存済みファイル（取り込んだ図面）のダウンロード。工事の閲覧権限が必要で、操作ログに記録する。</summary>
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/files/{id:guid}", async (Guid id, IAppDbContextFactory dbFactory, IFileStore store, IAuditLogger audit, CancellationToken ct) =>
        {
            await using var db = await dbFactory.CreateAsync(ct);
            var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
            if (file is null) return Results.NotFound();
            await audit.LogAsync(new AuditEntry(AuditCategory.Action, "FileDownload", $"図面をダウンロード：{file.FileName}")
            {
                EntityType = "StoredFile", EntityId = file.Id.ToString(),
            }, ct);
            return Results.Stream(store.OpenRead(file.StorageKey), file.ContentType, file.FileName);
        }).RequireAuthorization(Permissions.Projects.View);
    }
}
