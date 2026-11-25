using Jw.Application.Budgets;
using Jw.Domain.Common;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Jw.Infrastructure.Export;

/// <summary>
/// 见積書导出。模板沿用旧版 template.xlsx（嵌入资源），格子位置与旧版 JwBudgePage.ExcelExporter 一致：
/// 第 11 行（索引 10）是明细样式行；A～D 名称、E 仕様、G 数量、I 単位、J 単価、K 金額；
/// A1 标题后接工事名，O2 日期；明细之后依次是 小計・合計・消費税・総合計。
/// </summary>
internal sealed class QuotationExporter : IQuotationExporter
{
    private const string ResourceName = "Jw.Infrastructure.Templates.quotation-template.xlsx";
    private const int TemplateRow = 10;

    public byte[] Export(BudgetDetail budget)
    {
        using var template = typeof(QuotationExporter).Assembly.GetManifestResourceStream(ResourceName)
                             ?? throw new InvalidOperationException("見積書テンプレートが見つかりません。");
        var book = new XSSFWorkbook(template);
        var sheet = book.GetSheetAt(0);

        var title = sheet.GetRow(0).GetCell(0);
        title.SetCellValue(title.StringCellValue + budget.ProjectName);
        var date = sheet.GetRow(1).GetCell(14);
        date.SetCellType(CellType.String);
        date.SetCellValue(Shared.TimeOf(budget.CreatedAt).ToString("yyyy/MM/dd"));

        var i = 1;
        foreach (var line in budget.Lines.OrderBy(l => l.Kind))
        {
            var row = sheet.CopyRow(TemplateRow, TemplateRow + i);
            if (line.Kind == MaterialKind.Pillar)
            {
                // 柱：A 列放种类名，B 列放材料名（与旧版一致）
                if (!string.IsNullOrEmpty(line.CategoryName)) row.GetCell(0).SetCellValue(line.CategoryName);
                row.GetCell(1).SetCellValue(line.ItemName);
            }
            else if (!string.IsNullOrEmpty(line.ItemName))
            {
                row.GetCell(0).SetCellValue(line.ItemName);
                sheet.AddMergedRegion(new CellRangeAddress(TemplateRow + i, TemplateRow + i, 0, 3));
            }
            if (!string.IsNullOrEmpty(line.Spec)) row.GetCell(4).SetCellValue(line.Spec);
            row.GetCell(6).SetCellValue(line.Quantity.ToString("0.###"));
            if (!string.IsNullOrEmpty(line.UnitName)) row.GetCell(8).SetCellValue(line.UnitName);
            row.GetCell(9).SetCellValue(line.UnitPrice.ToString("0.##"));
            row.GetCell(10).SetCellValue((double)line.Amount);
            i++;
        }

        var subtotal = (double)budget.Subtotal;
        AddSummaryRow(sheet, TemplateRow + i, "小  計", subtotal);
        AddSummaryRow(sheet, TemplateRow + i + 1, "合  計", subtotal);
        var taxRow = AddSummaryRow(sheet, TemplateRow + i + 2, "消費税", (double)budget.Tax);
        taxRow.GetCell(6).SetCellValue((budget.TaxRate * 100).ToString("0"));
        taxRow.GetCell(8).SetCellValue("%");
        AddSummaryRow(sheet, TemplateRow + i + 3, "総  合  計", (double)budget.Total);

        using var ms = new MemoryStream();
        book.Write(ms, leaveOpen: true);
        return ms.ToArray();
    }

    private static IRow AddSummaryRow(ISheet sheet, int index, string label, double amount)
    {
        var row = sheet.CopyRow(TemplateRow, index);
        row.GetCell(0).SetCellValue(label);
        sheet.AddMergedRegion(new CellRangeAddress(index, index, 0, 3));
        row.GetCell(10).SetCellValue(amount);
        return row;
    }

    private static class Shared
    {
        private static readonly TimeZoneInfo Tokyo = FindTokyo();

        public static DateTime TimeOf(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tokyo);

        private static TimeZoneInfo FindTokyo()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        }
    }
}
