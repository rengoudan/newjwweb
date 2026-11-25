using System.Collections.Concurrent;
using System.Threading.Channels;
using Jw.Application.Floors;
using Jw.Cad.Parsing;
using JwShapeCommon;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jw.Infrastructure.Parsing;

public sealed class ParseOptionsConfig
{
    /// <summary>同時に実行する解析の数（CPU を多く使うため、既定はコア数の半分・最低 1）。</summary>
    public int MaxConcurrency { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
    /// <summary>解析結果を保存せずに保持する時間（分）。</summary>
    public int KeepResultMinutes { get; set; } = 60;
}

/// <summary>
/// 図面解析のバックグラウンド実行。キューに入ったジョブを最大 MaxConcurrency 件ずつ並行で処理する。
/// 解析器（Jw.Cad）は静的な状態を持たないので、複数の解析を同時に実行しても互いに影響しない。
/// </summary>
internal sealed class ParseJobRunner(IOptions<ParseOptionsConfig> options, ILogger<ParseJobRunner> logger)
    : BackgroundService, IParseJobs
{
    private readonly Channel<ParseJob> _queue = Channel.CreateUnbounded<ParseJob>();
    private readonly ConcurrentDictionary<Guid, ParseJob> _jobs = new();

    public void Enqueue(ParseJob job)
    {
        _jobs[job.Id] = job;
        if (!_queue.Writer.TryWrite(job)) throw new InvalidOperationException("解析キューに登録できませんでした。");
    }

    public ParseJob? Get(Guid jobId) => _jobs.GetValueOrDefault(jobId);

    public void Remove(Guid jobId) => _jobs.TryRemove(jobId, out _);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, Math.Max(1, options.Value.MaxConcurrency))
            .Select(_ => Task.Run(() => WorkerAsync(stoppingToken), stoppingToken)).ToList();
        workers.Add(CleanupAsync(stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task WorkerAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct))
        {
            if (!_jobs.ContainsKey(job.Id)) continue; // 開始前に破棄された
            job.MarkRunning();
            try
            {
                var progress = new InlineProgress<ParseStepInfo>(job.ReportStep);
                var result = FloorParser.Parse(job.FilePath, job.FloorName, job.Options, progress, logger);
                job.Complete(result);
                logger.LogInformation("図面解析完了 {File} 梁 {Beams} 柱 {Pillars} {Ms}ms",
                    job.FileName, result.Beams.Count, result.Pillars.Count, result.ElapsedMs);
            }
            catch (Exception ex)
            {
                // ParseStepException は「どの手順で失敗したか」を含む日本語メッセージ
                var message = ex is ParseStepException ? ex.Message : $"図面の解析に失敗しました：{ex.Message}";
                logger.LogWarning(ex, "図面解析失敗 {File}", job.FileName);
                job.Fail(message);
            }
        }
    }

    /// <summary>保存されずに残った解析結果を一定時間後に破棄する（メモリ解放）。</summary>
    private async Task CleanupAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var limit = DateTime.UtcNow.AddMinutes(-options.Value.KeepResultMinutes);
                foreach (var (id, job) in _jobs)
                    if (job.CompletedAt is { } done && done < limit) _jobs.TryRemove(id, out _);
            }
        }
        catch (OperationCanceledException) { }
    }

    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
