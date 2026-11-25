namespace JwShapeCommon
{
    /// <summary>
    /// 旧版 JwFileConsts 中从未被修改的值：部材的实物尺寸（mm）、识别容差等。
    /// 改为常量后没有任何运行期状态。数值与旧版完全一致。
    /// </summary>
    public static class JwConstants
    {
        public const double MaxErrorValue = 5;

        /// <summary>设计图角度 梁的宽度，只有上下面</summary>
        public const double BeamWidth = 100;

        /// <summary>VPL 链接件的宽度（56/2 + 68）</summary>
        public const double VPLCompentWidth = 96;

        /// <summary>未除 scale 的最小链接件间距</summary>
        public const double LianjieSpacing = 43;

        /// <summary>用来判定角度误差</summary>
        public const double JiaoduWuchazhi = 0.5;

        /// <summary>连接头的偏移判断值 单位为 mm（11 + 螺母 11）</summary>
        public const double LianjiePianyiPanding = 22;

        public const double PillarCenterMax = 8;

        /// <summary>K 柱实际宽度（mm）</summary>
        public const double KWidth = 1000;

        /// <summary>K 柱宽度容差（mm）</summary>
        public const double KErrorMargin = 60;

        public const int BRParseStyle = 1;

        /// <summary>文字中点 和所标识对象中间距离最大偏差值</summary>
        public const double TextParseMaxDistance = 100;

        public const double TextParseChuizhiMaxDistance = 100;

        public const double Lianghoudu = 5.5;

        // G 端口
        public const double Gjubian = 35;
        /// <summary>无论是 35 还是 27 间距都是 5</summary>
        public const double Gjianju = 5;
        public const double GBianjuZhongxin = 55;

        /// <summary>B 构建 孔间距</summary>
        public const double Kongjing = 56;

        public const double PianchaLianjieValue = 84;

        /// <summary>
        /// jww 坐标读取保留的小数位数。旧版画面和数据库里虽有同名设定，但从未赋值，实际始终为 6。
        /// </summary>
        public const int PickPrecision = 6;
        /// <summary>现实里的孔距（mm）</summary>
        public const double EllipseSpacing = 56;

        /// <summary>标准间隙（mm），程序里需要除 scale</summary>
        public const double JwJianxi = 120;

        public const double GoujianWidth = 100;

        public const double GoujianHeight = 48;

        /// <summary>梁识别范围的基准值（加上导入时指定的补正后使用）</summary>
        public const double MaxBeamScope = 100;
    }
}
