using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Jw.Application.Abstractions;
using Jw.Application.Auditing;
using Jw.Application.Common;
using Jw.Application.Customers;
using Jw.Application.Files;
using Jw.Cad.Parsing;
using Jw.Domain.Auditing;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Jw.Domain.Files;
using Jw.Domain.Projects;
using JwCore;
using ProjectStatus = Jw.Domain.Common.ProjectStatus;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Floors;

public sealed record UploadedFile(Guid Id, string FileName, long Size);

/// <summary>取り込み画面の初期表示に必要な情報。</summary>
public sealed record ImportContext(
    Guid ProjectId, string ProjectName, string? CustomerName, Guid? CustomerId,
    string? BlockedReason, ParseSettingInput? CustomerSettings, IReadOnlyList<string> ExistingFloors, string SuggestedFloorName,
    IReadOnlyList<BeamMaterialOption> BeamMaterials, Guid? DefaultBeamMaterialId);

public sealed record BeamMaterialOption(Guid Id, string Name);

/// <summary>取り込みごとに指定する項目（旧版の設定画面で取り込み時に入力していたもの）。</summary>
public sealed class ImportInput
{
    public Guid ProjectId { get; set; }
    public Guid FileId { get; set; }

    [Required(ErrorMessage = "階名を入力してください。")]
    [StringLength(100)]
    public string FloorName { get; set; } = "";

    /// <summary>K 柱種別（900 / 910）。</summary>
    public KPillarType KPillarType { get; set; } = KPillarType.A900;
    /// <summary>穴あけ方式：最少打孔。</summary>
    public bool MinimalStrategy { get; set; }
    /// <summary>追加の穴の線色（-1 = なし）。</summary>
    public int ExtraHoleColor { get; set; } = -1;
    /// <summary>梁の識別範囲の補正（-100～100）。</summary>
    [Range(-100, 100)]
    public double BeamScopeOffset { get; set; }
    /// <summary>面積 0 の候補矩形を除外する（推奨）。</summary>
    public bool ExcludeDegenerateRectangles { get; set; } = true;
    /// <summary>梁の材料（旧版「モジュール」）。予算の自動作成で梁の単価に使う。</summary>
    public Guid? BeamMaterialId { get; set; }
}

/// <summary>
/// 図面の取り込み：アップロード → 解析ジョブ開始（バックグラウンド）→ 結果確認 → 保存。
/// 解析設定は工事の顧客の「解析設定」に、取り込みごとの指定を加えたもの。
/// </summary>
public sealed class FloorImportService(
    IAppDbContextFactory dbFactory, ICurrentUser currentUser, IFileStore files, IParseJobs jobs,
    IAuditLogger audit, TimeProvider clock)
{
    public const long MaxFileSize = 50L * 1024 * 1024;

    public async Task<UploadedFile> UploadAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Import);
        if (!string.Equals(Path.GetExtension(fileName), ".jww", StringComparison.OrdinalIgnoreCase))
            throw new AppException("JW_CAD の図面ファイル（.jww）を選択してください。");

        // ハッシュを計算しながら保存する
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new AppException("ファイルが空です。");
        if (buffer.Length > MaxFileSize) throw new AppException("ファイルが大きすぎます（上限 50MB）。");
        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
        buffer.Position = 0;
        var key = await files.SaveAsync(buffer, ".jww", ct);

        await using var db = await dbFactory.CreateAsync(ct);
        var file = new StoredFile
        {
            FileName = Path.GetFileName(fileName),
            ContentType = "application/octet-stream",
            Size = buffer.Length,
            Sha256 = hash,
            StorageKey = key,
            Purpose = "drawing",
            UploadedAt = clock.GetUtcNow().UtcDateTime,
            UploadedById = currentUser.Id,
            UploadedByName = currentUser.UserName,
        };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return new UploadedFile(file.Id, file.FileName, file.Size);
    }

    public async Task<ImportContext> PrepareAsync(Guid projectId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Import);
        await using var db = await dbFactory.CreateAsync(ct);
        var p = await db.Projects.AsNoTracking().Include(x => x.Customer).ThenInclude(c => c!.ParseSetting)
                    .FirstOrDefaultAsync(x => x.Id == projectId, ct) ?? throw new NotFoundException("工事");
        var floors = await db.Floors.AsNoTracking().Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.CreatedAt).Select(f => f.Name).ToListAsync(ct);

        ParseSettingInput? settings = null;
        if (p.Customer?.ParseSetting is { } s)
            settings = new ParseSettingInput
            {
                CustomerId = s.CustomerId, Exists = true,
                BeamColor = s.BeamColor, BeamTextColor = s.BeamTextColor, PillarColor = s.PillarColor, PillarPenStyle = s.PillarPenStyle,
                PillarRecognition = s.PillarRecognition, SplitColor = s.SplitColor, SplitPenStyle = s.SplitPenStyle,
                SplitRecognition = s.SplitRecognition, NearSplitMax = s.NearSplitMax, DownPillarColor = s.DownPillarColor,
                BraceColor = s.BraceColor, BraceOrigin = s.BraceOrigin, BraceComponent = s.BraceComponent,
            };

        var blocked = p.ImportBlockedReason()
                      ?? (p.CustomerId is null ? "工事に顧客が設定されていません。工事を編集して顧客を選んでください。" : null)
                      ?? (settings is null ? $"顧客「{p.Customer!.CompanyName}」の解析設定がありません。顧客画面で解析設定を登録してください。" : null);

        var suggested = Enumerable.Range(1, 200).Select(i => $"{i}階").First(n => !floors.Contains(n));
        var beamMaterials = await db.Materials.AsNoTracking().Where(m => m.Kind == MaterialKind.Beam)
            .OrderBy(m => m.Name).ThenBy(m => m.Spec).Select(m => new BeamMaterialOption(m.Id, m.Name + (m.Spec != null ? " " + m.Spec : ""))).ToListAsync(ct);
        var defaultBeam = await db.MaterialCategories.AsNoTracking().Where(c => c.Kind == MaterialKind.Beam && c.DefaultMaterialId != null)
            .Select(c => c.DefaultMaterialId).FirstOrDefaultAsync(ct);
        return new ImportContext(p.Id, p.Name, p.Customer?.CompanyName, p.CustomerId, blocked, settings, floors, suggested,
            beamMaterials, defaultBeam ?? beamMaterials.FirstOrDefault()?.Id);
    }

    /// <summary>解析ジョブを開始してジョブ ID を返す。</summary>
    public async Task<Guid> StartAsync(ImportInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Import);
        var ctx = await PrepareAsync(input.ProjectId, ct);
        if (ctx.BlockedReason is not null) throw new AppException(ctx.BlockedReason);
        var floorName = input.FloorName.Trim();
        if (ctx.ExistingFloors.Contains(floorName)) throw new AppException($"階「{floorName}」は既に取り込まれています。");

        await using var db = await dbFactory.CreateAsync(ct);
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.FileId, ct)
                   ?? throw new NotFoundException("図面ファイル");

        var s = ctx.CustomerSettings!;
        var options = new ParseOptions
        {
            BeamColor = s.BeamColor, BeamTextColor = s.BeamTextColor,
            PillarColor = s.PillarColor, PillarPenStyle = s.PillarPenStyle, PillarRecognition = (PillarDrawingMethod)s.PillarRecognition,
            SplitColor = s.SplitColor, SplitPenStyle = s.SplitPenStyle, SplitRecognition = (PillarDrawingMethod)s.SplitRecognition,
            NearSplitMax = s.NearSplitMax, DownPillarColor = s.DownPillarColor, BraceColor = s.BraceColor,
            BraceOrigin = (LianjieParsingMethod)s.BraceOrigin, BraceComponent = (LianjieCompentType)s.BraceComponent,
            KPillarType = input.KPillarType, MinimalStrategy = input.MinimalStrategy, ExtraHoleColor = input.ExtraHoleColor,
            BeamScopeOffset = input.BeamScopeOffset, ExcludeDegenerateRectangles = input.ExcludeDegenerateRectangles,
        };

        var job = new ParseJob
        {
            Id = Guid.NewGuid(), ProjectId = ctx.ProjectId, FloorName = floorName, FileId = file.Id, FileName = file.FileName,
            FilePath = files.GetFullPath(file.StorageKey), Options = options, BeamMaterialId = input.BeamMaterialId,
            OwnerUserId = currentUser.Id, OwnerUserName = currentUser.UserName,
        };
        jobs.Enqueue(job);

        await audit.LogAsync(new AuditEntry(AuditCategory.Action, "ParseStart", $"図面解析を開始：{ctx.ProjectName} {floorName}（{file.FileName}）")
        {
            EntityType = nameof(Project), EntityId = ctx.ProjectId.ToString(), Detail = options,
        }, ct);
        return job.Id;
    }

    public ParseJob GetJob(Guid jobId)
    {
        currentUser.Require(Permissions.Projects.Import);
        return jobs.Get(jobId) ?? throw new AppException("解析結果が見つかりません。時間がたって破棄された可能性があります。もう一度取り込んでください。");
    }

    public void Discard(Guid jobId)
    {
        currentUser.Require(Permissions.Projects.Import);
        jobs.Remove(jobId);
    }

    /// <summary>解析結果を階として保存し、工事の集計を更新する。</summary>
    public async Task<Guid> SaveAsync(Guid jobId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Projects.Import);
        var job = GetJob(jobId);
        if (job.Status != ParseJobStatus.Succeeded || job.Result is null) throw new AppException("解析が完了していません。");
        if (!job.TryMarkSaved()) throw new AppException("この解析結果は既に保存されています。");

        try
        {
            await using var db = await dbFactory.CreateAsync(ct);
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == job.ProjectId, ct) ?? throw new NotFoundException("工事");
            if (project.ImportBlockedReason() is { } blocked) throw new AppException(blocked);
            if (await db.Floors.AnyAsync(f => f.ProjectId == project.Id && f.Name == job.FloorName, ct))
                throw new AppException($"階「{job.FloorName}」は既に取り込まれています。");

            var r = job.Result;
            var sub = r.Floor;
            var floor = new Floor
            {
                ProjectId = project.Id, Name = job.FloorName, SourceFileId = job.FileId,
                BeamCount = r.Beams.Count,
                HorizontalBeamsCount = r.Beams.Count(b => b.DirectionType == BeamDirectionType.Horizontal),
                VerticalBeamsCount = r.Beams.Count(b => b.DirectionType == BeamDirectionType.Vertical),
                PillarCount = r.Pillars.Count,
                KPillarCount = r.Pillars.Count(p => p.BaseType == PillarBaseType.KPillar),
                SinglePillarCount = r.Pillars.Count(p => p.BaseType == PillarBaseType.SinglePillar),
                BCount = sub.BCount, BGCount = sub.BGCount,
                Scale = sub.Scale, ScaleText = sub.Biaochi, MarkBeam = sub.MarkBeam,
                LocationX = sub.Location?.X ?? 0, LocationY = sub.Location?.Y ?? 0, Width = sub.Width, Height = sub.Height,
                DirectionType = (int)sub.DirectionType,
                KPillarType = (int)sub.KPillarType, BraceComponent = (int)sub.LianjieCompentType, MinimalStrategy = sub.MinimalStrategy,
                ParseOptionsJson = JsonSerializer.Serialize(job.Options),
                WarningsJson = r.Warnings.Count > 0 ? JsonSerializer.Serialize(r.Warnings) : null,
                ParseElapsedMs = r.ElapsedMs,
            };
            db.Floors.Add(floor);

            // 明细的所属楼层改为新 Floor 的 Id（解析器生成时是临时的 JwProjectSubData.Id）
            var floorKey = floor.Id.ToString();
            var beamMaterial = job.BeamMaterialId is { } mid ? await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mid, ct) : null;
            foreach (var b in r.Beams)
            {
                b.JwProjectSubDataId = floorKey;
                b.FloorName = job.FloorName;
                // 旧版：取り込み時に選んだ「モジュール」（梁材料）を全梁に設定
                b.BeamXHId = beamMaterial?.Id.ToString() ?? "";
                b.BeamXHName = beamMaterial?.GeneralTitle ?? "";
            }
            foreach (var x in r.Pillars) x.JwProjectSubDataId = floorKey;
            foreach (var x in r.LinkParts) x.JwProjectSubDataId = floorKey;
            foreach (var x in r.Lianjies) { x.JwProjectSubDataId = floorKey; x.ProjectSubName = job.FloorName; }
            foreach (var x in r.DownPillars) x.JwProjectSubDataId = floorKey;
            foreach (var x in r.Cuttings) x.JwProjectSubDataId = floorKey;
            foreach (var x in r.AddedHoleMarks) x.JwProjectSubDataId = floorKey;
            ParseRows.AddInOrder(db, db.FloorBeams, r.Beams);
            ParseRows.AddInOrder(db, db.FloorHoles, r.Holes);
            ParseRows.AddInOrder(db, db.FloorBeamVerticals, r.BeamVerticals);
            ParseRows.AddInOrder(db, db.FloorPillars, r.Pillars);
            ParseRows.AddInOrder(db, db.FloorLinkParts, r.LinkParts);
            ParseRows.AddInOrder(db, db.FloorLianjies, r.Lianjies);
            ParseRows.AddInOrder(db, db.FloorDownPillars, r.DownPillars);
            ParseRows.AddInOrder(db, db.FloorCuttings, r.Cuttings);
            ParseRows.AddInOrder(db, db.FloorAddedHoleMarks, r.AddedHoleMarks);

            await ProjectStats.RecalculateAsync(db, project, addedFloor: floor, ct: ct);
            await db.SaveChangesAsync(ct);

            await audit.LogAsync(new AuditEntry(AuditCategory.Action, "FloorImport",
                $"図面を取り込み：{project.Name} {floor.Name}（梁 {floor.BeamCount}・柱 {floor.PillarCount}・B {floor.BCount}・BG {floor.BGCount}）")
            {
                EntityType = nameof(Floor), EntityId = floor.Id.ToString(),
                Detail = new { job.FileName, floor.BeamCount, floor.PillarCount, floor.BCount, floor.BGCount, Lianjie = r.Lianjies.Count, r.ElapsedMs },
            }, ct);

            jobs.Remove(jobId);
            return floor.Id;
        }
        catch
        {
            job.UndoSaved();
            throw;
        }
    }
}

/// <summary>工事の集計（解析済み階数・梁数・柱数・B/BG）を階の集計から再計算する。旧版 ReCalculateMain。</summary>
internal static class ProjectStats
{
    public static async Task RecalculateAsync(IAppDbContext db, Project project, Floor? addedFloor = null, Guid? removedFloorId = null, CancellationToken ct = default)
    {
        var floors = await db.Floors.Where(f => f.ProjectId == project.Id && f.Id != removedFloorId).ToListAsync(ct);
        if (addedFloor is not null && floors.All(f => f.Id != addedFloor.Id)) floors.Add(addedFloor);

        project.ParsedQuantity = floors.Count;
        project.BeamCount = floors.Sum(f => f.BeamCount);
        project.PillarCount = floors.Sum(f => f.PillarCount);
        project.KPillarCount = floors.Sum(f => f.KPillarCount);
        project.SinglePillarCount = floors.Sum(f => f.SinglePillarCount);
        project.BCount = floors.Sum(f => f.BCount);
        project.BGCount = floors.Sum(f => f.BGCount);
        project.Scale = floors.FirstOrDefault()?.ScaleText;
        if (project.Status == ProjectStatus.Created && floors.Count > 0) project.Status = ProjectStatus.Parsed;
        if (project.Status == ProjectStatus.Parsed && floors.Count == 0) project.Status = ProjectStatus.Created;
    }
}
