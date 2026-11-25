namespace JwShapeCommon.Jwbase
{
    /// <summary>JW_CAD 线色（lc0～lc14，-1 = 未设定）。不可变，按番号取共享实例。</summary>
    public sealed class JwColor
    {
        private JwColor(int number, string name)
        {
            ColorNumber = number;
            JwColorName = name;
        }

        public string JwColorName { get; }
        public int ColorNumber { get; }

        public static IReadOnlyList<JwColor> All { get; } =
            Enumerable.Range(-1, 16).Select(i => new JwColor(i, i == -1 ? "線⾊番号" : $"lc{i}")).ToList();

        /// <summary>番号对应的线色；范围外视为未设定（-1）。</summary>
        public static JwColor Of(int number) => All[number is >= -1 and <= 14 ? number + 1 : 0];
    }

    /// <summary>JW_CAD 线种（ls0～ls14，-1 = 未设定）。不可变，按番号取共享实例。</summary>
    public sealed class JwPenStyle
    {
        private JwPenStyle(int number, string name)
        {
            StyleNumber = number;
            JwPenStyleName = name;
        }

        public string JwPenStyleName { get; }
        public int StyleNumber { get; }

        public static IReadOnlyList<JwPenStyle> All { get; } =
            Enumerable.Range(-1, 16).Select(i => new JwPenStyle(i, i == -1 ? "線種番号" : $"ls{i}")).ToList();

        public static JwPenStyle Of(int number) => All[number is >= -1 and <= 14 ? number + 1 : 0];
    }
}
