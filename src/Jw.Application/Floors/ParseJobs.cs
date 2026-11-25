using Jw.Cad.Parsing;

namespace Jw.Application.Floors;

public enum ParseJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

/// <summary>
/// 図面解析ジョブ（メモリ上）。解析はバックグラウンドで実行し、結果はユーザーが「保存」か「破棄」を選ぶまで保持する
/// （旧版の「解析 → 内容確認 → 保存」と同じ流れ）。一定時間たつと自動的に破棄する。
/// </summary>
public sealed class ParseJob
{
    private readonly object _lock = new();
    private readonly List<ParseStepInfo> _steps = [];

    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string FloorName { get; init; }
    public required Guid FileId { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required ParseOptions Options { get; init; }
    public required Guid? OwnerUserId { get; init; }
    public Guid? BeamMaterialId { get; init; }
    public required string? OwnerUserName { get; init; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public ParseJobStatus Status { get; private set; } = ParseJobStatus.Queued;
    public string? Error { get; private set; }
    public FloorParseResult? Result { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    /// <summary>保存済み（同じ結果を二重に保存しない）。</summary>
    public bool Saved { get; private set; }

    /// <summary>状態が変わったとき（画面の更新用）。</summary>
    public event Action? Changed;

    public IReadOnlyList<ParseStepInfo> Steps
    {
        get { lock (_lock) return _steps.ToList(); }
    }

    public void MarkRunning() => Update(() => Status = ParseJobStatus.Running);

    public void ReportStep(ParseStepInfo step) => Update(() =>
    {
        var i = _steps.FindIndex(s => s.Index == step.Index);
        if (i >= 0) _steps[i] = step; else _steps.Add(step);
    });

    public void Complete(FloorParseResult result) => Update(() =>
    {
        Result = result;
        Status = ParseJobStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
    });

    public void Fail(string error) => Update(() =>
    {
        Error = error;
        Status = ParseJobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    });

    public bool TryMarkSaved()
    {
        lock (_lock)
        {
            if (Saved || Status != ParseJobStatus.Succeeded) return false;
            Saved = true;
            return true;
        }
    }

    /// <summary>保存に失敗したときに「保存済み」を取り消す（やり直せるように）。</summary>
    public void UndoSaved()
    {
        lock (_lock) Saved = false;
    }

    private void Update(Action change)
    {
        lock (_lock) change();
        Changed?.Invoke();
    }
}

/// <summary>解析ジョブの受付と保持（実装は Infrastructure のバックグラウンドサービス）。</summary>
public interface IParseJobs
{
    void Enqueue(ParseJob job);
    ParseJob? Get(Guid jobId);
    void Remove(Guid jobId);
}
