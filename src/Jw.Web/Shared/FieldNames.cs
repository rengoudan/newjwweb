namespace Jw.Web.Shared;

/// <summary>审计明细里字段名的显示名。新增业务字段时在这里补充，没有的按原名显示。</summary>
public static class FieldNames
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["CompanyName"] = "会社名",
        ["Address"] = "住所",
        ["Contact"] = "担当者",
        ["Telephone"] = "電話番号",
        ["Note"] = "備考",
        ["IsDeleted"] = "削除",
        ["DisplayName"] = "表示名",
        ["Email"] = "メール",
        ["IsActive"] = "有効",
        ["Roles"] = "ロール",
        ["Permissions"] = "権限",
        ["Description"] = "説明",
    };

    public static string Of(string property) => Map.GetValueOrDefault(property, property);
}
