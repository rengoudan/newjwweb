using JwCore;
using JwShapeCommon.Jwbase;

namespace JwShapeCommon
{
    /// <summary>
    /// 一次解析使用的设定（不可变）。旧版放在 JwFileConsts 的静态字段里，由设定画面在解析前写入；
    /// 现在作为参数显式传入解析器，多个解析可以同时进行、互不影响。
    /// </summary>
    public sealed class ParseSettings
    {
        /// <summary>梁輪郭線の線色。</summary>
        public JwColor BeamParseColor { get; init; } = JwColor.Of(-1);
        /// <summary>注釈（梁符号）の線色。</summary>
        public JwColor BeamSymbolTextColor { get; init; } = JwColor.Of(-1);
        /// <summary>継手記号の線色。</summary>
        public JwColor BeamSplitParseColor { get; init; } = JwColor.Of(-1);
        /// <summary>柱の線色。</summary>
        public JwColor BeamPillarParseColor { get; init; } = JwColor.Of(-1);
        public JwPenStyle PillarPenStyle { get; init; } = JwPenStyle.Of(-1);
        public JwPenStyle SplitPenStyle { get; init; } = JwPenStyle.Of(-1);
        /// <summary>ブレースの線色。</summary>
        public JwColor LianjieParseColor { get; init; } = JwColor.Of(-1);
        /// <summary>追加の穴の線色。</summary>
        public JwColor EWaiKongColor { get; init; } = JwColor.Of(-1);
        /// <summary>下階柱記号の線色番号。</summary>
        public int BRParseColore { get; init; } = 1;

        /// <summary>識別範囲エラー。</summary>
        public double NearSpliteMax { get; init; } = 1;
        /// <summary>梁の識別範囲（基準値 + 導入時の補正）。</summary>
        public double MaxBeamScope { get; init; } = JwConstants.MaxBeamScope;

        public LianjieParsingMethod LianjieParsingMethod { get; init; } = LianjieParsingMethod.OnBeam;
        public PillarDrawingMethod PillarDrawingMethod { get; init; } = PillarDrawingMethod.Shape;
        public PillarDrawingMethod SpliteDrawingMethod { get; init; } = PillarDrawingMethod.Shape;
        public KPillarType Ktype { get; init; } = KPillarType.A900;
        public LianjieCompentType LjCompentType { get; init; } = LianjieCompentType.BR8;
        /// <summary>最少打孔策略。</summary>
        public bool MinimalStrategy { get; init; }
        /// <summary>識別時に退化矩形（面積 0）を除外する。</summary>
        public bool ExcludeDegenerateRectangles { get; init; }

        /// <summary>导出等不做识别的场合使用的默认设定。</summary>
        public static ParseSettings Default { get; } = new();
    }

    /// <summary>
    /// 出力（JW 図面・梁製造 CSV）の設定。旧版はアプリの設定画面（appconfig.json の Csv 節）で変更でき、
    /// 起動時に静的変数 JwFileConsts.EllipseDiameter / CsvYTiaozheng に入れていた。
    /// 新版はシステム設定（データベース）に保存し、出力のたびに渡す。
    /// </summary>
    public sealed class ExportSettings
    {
        /// <summary>孔径（mm）。旧版 Csv.Kongjing。図面の穴の円と CSV の孔径列に使う。</summary>
        public double HoleDiameter { get; init; } = 17;

        /// <summary>梁製造 CSV の Y 方向調整（mm）。旧版 Csv.Ytiaozheng。</summary>
        public double CsvYAdjust { get; init; }

        public static ExportSettings Default { get; } = new();
    }

    /// <summary>
    /// 一张图纸（一层）的上下文：缩尺来自文件头，属于图纸本身；加上解析设定。
    /// 不可变，由解析器在读完文件头后创建，传给该图纸内的所有图形对象（梁、孔、柱、连接件…）。
    /// 从数据库重建画布时，用保存的缩尺新建一个。旧版这些值是进程级静态变量 JwFileConsts.JwScale 等。
    /// </summary>
    public sealed class JwDrawing
    {
        public JwDrawing(double scale, ParseSettings settings = null, ExportSettings export = null)
        {
            if (!(scale > 0)) throw new ArgumentOutOfRangeException(nameof(scale), scale, "縮尺が不正です。");
            Scale = scale;
            Settings = settings ?? ParseSettings.Default;
            Export = export ?? ExportSettings.Default;
            Precision = (int)Math.Log(scale, 10);
        }

        /// <summary>图纸缩尺（1/Scale）。旧版 JwFileConsts.JwScale。</summary>
        public double Scale { get; }

        /// <summary>按缩尺保留的小数位数。旧版 JwFileConsts.JiangjingduInt。</summary>
        public int Precision { get; }

        public ParseSettings Settings { get; }

        /// <summary>出力の設定（孔径・CSV 調整）。</summary>
        public ExportSettings Export { get; }

        /// <summary>
        /// 图纸内对象之间的通知（例如梁识别出连接件时通知解析器）。旧版是进程级单例 GlobalEvent，
        /// 所有解析共享且订阅从不取消；现在每张图纸一个，只在本图纸内流动。
        /// </summary>
        public GlobalEvent Events { get; } = new GlobalEvent();
    }

    /// <summary>解析日志（旧版 JwLogging.AppLog 的实例版，模板语法相同）。</summary>
    public sealed class ParseLog
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public ParseLog(Microsoft.Extensions.Logging.ILogger logger) =>
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

#pragma warning disable CA2254 // 模板来自旧代码，保持原样
        public void Debug(string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogDebug(_logger, template, args);
        public void Info(string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(_logger, template, args);
        public void Warn(string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, template, args);
        public void Warn(Exception ex, string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, ex, template, args);
        public void Error(string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogError(_logger, template, args);
        public void Error(Exception ex, string template, params object[] args) => Microsoft.Extensions.Logging.LoggerExtensions.LogError(_logger, ex, template, args);
#pragma warning restore CA2254
    }
}
