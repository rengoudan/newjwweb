using JwCore;
using JwShapeCommon;
using JwShapeCommon.Jwbase;
using Microsoft.Extensions.Logging;

namespace Jw.Cad.Parsing;

/// <summary>
/// 一次图纸解析的参数：顧客的解析設定 + 本次导入时指定的项目（与旧版 CustomerDesignSetting 画面对应）。
/// 线色、线种番号为 JW_CAD 的 lc / ls 番号，-1 表示未设定。
/// </summary>
public sealed record ParseOptions
{
    public int BeamColor { get; init; } = -1;
    public int BeamTextColor { get; init; } = -1;
    public int PillarColor { get; init; } = -1;
    public int PillarPenStyle { get; init; } = -1;
    public PillarDrawingMethod PillarRecognition { get; init; } = PillarDrawingMethod.Line;
    public int SplitColor { get; init; } = -1;
    public int SplitPenStyle { get; init; } = -1;
    public PillarDrawingMethod SplitRecognition { get; init; } = PillarDrawingMethod.Line;
    public double NearSplitMax { get; init; } = 1;
    public int DownPillarColor { get; init; } = -1;
    public int BraceColor { get; init; } = -1;
    public LianjieParsingMethod BraceOrigin { get; init; } = LianjieParsingMethod.OnBeam;
    public LianjieCompentType BraceComponent { get; init; } = LianjieCompentType.BR8;

    // ---- 每次导入时指定 ----
    public KPillarType KPillarType { get; init; } = KPillarType.A900;
    /// <summary>追加の穴の線色。</summary>
    public int ExtraHoleColor { get; init; } = -1;
    /// <summary>穴あけ方式：最少打孔。</summary>
    public bool MinimalStrategy { get; init; }
    /// <summary>梁の識別範囲の補正（旧版画面の数値、-100～）。</summary>
    public double BeamScopeOffset { get; init; }
    /// <summary>識別時に退化矩形（面積 0）を除外する（旧版の試験オプション）。</summary>
    public bool ExcludeDegenerateRectangles { get; init; }

    /// <summary>转换为解析器使用的不可变设定（未列出的项目取旧版默认值）。</summary>
    public ParseSettings ToSettings() => new()
    {
        BeamParseColor = JwColor.Of(BeamColor),
        BeamSymbolTextColor = JwColor.Of(BeamTextColor),
        BeamSplitParseColor = JwColor.Of(SplitColor),
        BeamPillarParseColor = JwColor.Of(PillarColor),
        PillarPenStyle = JwPenStyle.Of(PillarPenStyle),
        SplitPenStyle = JwPenStyle.Of(SplitPenStyle),
        BRParseColore = DownPillarColor,
        LianjieParseColor = JwColor.Of(BraceColor),
        NearSpliteMax = NearSplitMax,
        LianjieParsingMethod = BraceOrigin,
        PillarDrawingMethod = PillarRecognition,
        SpliteDrawingMethod = SplitRecognition,
        Ktype = KPillarType,
        EWaiKongColor = JwColor.Of(ExtraHoleColor),
        LjCompentType = BraceComponent,
        MinimalStrategy = MinimalStrategy,
        ExcludeDegenerateRectangles = ExcludeDegenerateRectangles,
        // 旧版每次导入都在静态值上再加一次补正（导入越多偏差越大）；这里每次从基准值开始只加一次
        MaxBeamScope = JwConstants.MaxBeamScope + BeamScopeOffset,
    };
}

/// <summary>解析步骤的进度。</summary>
public sealed record ParseStepInfo(int Index, int Total, string StepName, string Status, long ElapsedMs, string? Summary);

/// <summary>一层图纸的解析结果（旧版 JwFileHandle.CreateData 的产物）。</summary>
public sealed class FloorParseResult
{
    public required JwProjectSubData Floor { get; init; }
    public required IReadOnlyList<JwBeamData> Beams { get; init; }
    public required IReadOnlyList<JwHoleData> Holes { get; init; }
    public required IReadOnlyList<JwBeamVerticalData> BeamVerticals { get; init; }
    public required IReadOnlyList<JwPillarData> Pillars { get; init; }
    public required IReadOnlyList<JwLinkPartData> LinkParts { get; init; }
    public required IReadOnlyList<JwLianjieData> Lianjies { get; init; }
    public required IReadOnlyList<JwDownPillarData> DownPillars { get; init; }
    public required IReadOnlyList<JwCutting> Cuttings { get; init; }
    public required IReadOnlyList<JwAddedHoleMarkData> AddedHoleMarks { get; init; }

    /// <summary>解析日志（旧版解析窗口里显示的内容）。</summary>
    public required IReadOnlyList<string> Messages { get; init; }
    /// <summary>需要用户注意的提示（例如某种颜色识别不到梁/柱）。</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
    /// <summary>解析中被降级处理的异常，按类别计数。</summary>
    public required IReadOnlyDictionary<string, int> SuppressedErrors { get; init; }
    public long ElapsedMs { get; init; }

    /// <summary>解析后的画布（第 4 阶段用于预览与导出）。</summary>
    public JwCanvas? Canvas { get; init; }
}

/// <summary>
/// 图纸解析入口：读取 .jww → 按旧版顺序执行全部识别步骤 → 生成数据。
/// 不使用任何静态可变状态：设定、缩尺、事件、日志都属于本次解析的对象，可以在多个线程同时调用。
/// </summary>
public static class FloorParser
{
    public static FloorParseResult Parse(string path, string floorName, ParseOptions options,
        IProgress<ParseStepInfo>? progress = null, ILogger? logger = null)
    {
        var model = new JwProjectPathModel
        {
            Path = path,
            FloorName = floorName,
            MainData = new JwProjectMainData { Id = 0, ProjectName = "" },
        };
        var handle = new JwFileHandle(model, options.ToSettings(), logger);
        if (progress is not null)
            handle.Progress = new SyncProgress<ParseProgress>(p => progress.Report(new ParseStepInfo(
                p.Index, p.Total, p.StepName, p.Status.ToString(), p.ElapsedMs, p.Summary)));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        handle.ReadJwFile();
        handle.FollowTheStep();
        var canvas = handle.BuildCanvas(); // 计算图纸范围（CreateData 依赖），同时得到画布
        handle.CreateData();

        var messages = handle.Messages;
        var warnings = messages.Where(m => m.StartsWith('⚠') || m.Contains("できません") || m.StartsWith("【エラー】")).Distinct().ToList();
        return new FloorParseResult
        {
            Floor = handle._subData,
            Beams = handle._beamdatas,
            Holes = handle._holeDatas,
            BeamVerticals = handle._jwbvdatas,
            Pillars = handle._beampillarDatas,
            LinkParts = handle._linkPartDatas,
            Lianjies = handle._lianjieDatas,
            DownPillars = handle._downPillarDatas,
            Cuttings = handle._cuttingDatas,
            AddedHoleMarks = handle._addMarkDatas,
            Messages = messages,
            Warnings = warnings,
            SuppressedErrors = new Dictionary<string, int>(handle.SuppressedErrors),
            ElapsedMs = sw.ElapsedMilliseconds,
            Canvas = canvas,
        };
    }

    /// <summary>同步回调的 IProgress（Progress&lt;T&gt; 会投递到线程池，顺序和时机不可控）。</summary>
    private sealed class SyncProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
