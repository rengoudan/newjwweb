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
        // 工事
        ["Name"] = "名称",
        ["SiteAddress"] = "建設現場住所",
        ["CustomerId"] = "顧客",
        ["FloorQuantity"] = "階数",
        ["ParsedQuantity"] = "解析済み階数",
        ["Status"] = "状態",
        ["Scale"] = "縮尺",
        // 材料
        ["Kind"] = "カテゴリー",
        ["CategoryId"] = "種類",
        ["DefaultMaterialId"] = "既定の材料",
        ["Spec"] = "仕様",
        ["GeneralTitle"] = "共通識別子",
        ["UnitName"] = "単位",
        ["UnitPrice"] = "単価",
        ["NumParameter"] = "数値パラメータ",
        ["Remark"] = "摘要",
        // 予算
        ["ProjectId"] = "工事",
        ["Amount"] = "金額",
        ["BudgetId"] = "予算",
        ["MaterialId"] = "材料",
        ["ItemName"] = "項目",
        ["Quantity"] = "数量",
        ["Source"] = "区分",
        // 解析設定
        ["BeamColor"] = "梁輪郭線の線色",
        ["BeamTextColor"] = "注釈の線色",
        ["PillarColor"] = "柱の線色",
        ["PillarPenStyle"] = "柱の線種",
        ["PillarRecognition"] = "柱識別方法",
        ["SplitColor"] = "継手記号の線色",
        ["SplitPenStyle"] = "継手記号の線種",
        ["SplitRecognition"] = "継手記号の識別方法",
        ["NearSplitMax"] = "識別範囲エラー",
        ["DownPillarColor"] = "下階柱記号の線色",
        ["BraceColor"] = "ブレースの線色",
        ["BraceOrigin"] = "ブレース起点",
        ["BraceComponent"] = "ブレース部材",
        ["PickPrecision"] = "拾い精度",
        ["Gap"] = "隙間",
    };

    public static string Of(string property) => Map.GetValueOrDefault(property, property);
}
