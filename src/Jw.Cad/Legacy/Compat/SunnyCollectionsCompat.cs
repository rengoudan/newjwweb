namespace JwShapeCommon.Compat;

/// <summary>
/// 旧版兼容：旧代码有 9 个文件 using 了 WinForms 界面库 Sunny.UI，
/// 该库带有 First()/Last()（参数为 IList&lt;T&gt;）扩展方法，比 LINQ 的同名方法更“具体”，
/// 编译器会优先选中它。它在列表为空时返回 default，而 LINQ 会抛异常。
/// 解析逻辑在多处依赖这一行为（例如 JwBeamExtensions.BeamSpliteDouble 里的 jwQiegeZus.Last()）。
/// 为保证解析结果与旧版一致，这里提供语义相同的实现，并且只在原先 using Sunny.UI 的文件中引入。
/// 只影响无参数的 First()/Last()；带条件的 First(predicate) 仍是 LINQ。
/// </summary>
public static class SunnyCollectionsCompat
{
    public static T First<T>(this IList<T> list) => list is { Count: > 0 } ? list[0] : default!;

    public static T Last<T>(this IList<T> list) => list is { Count: > 0 } ? list[^1] : default!;
}
