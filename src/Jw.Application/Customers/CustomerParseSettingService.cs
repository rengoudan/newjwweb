using System.ComponentModel.DataAnnotations;
using Jw.Application.Abstractions;
using Jw.Application.Common;
using Jw.Domain.Authorization;
using Jw.Domain.Common;
using Jw.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Jw.Application.Customers;

/// <summary>JW_CAD 的线色（lc0～lc14）和线种（ls0～ls14）番号；-1 为未设定。</summary>
public static class JwPen
{
    public static readonly IReadOnlyList<int> Numbers = Enumerable.Range(-1, 16).ToList();

    public static string ColorName(int n) => n < 0 ? "未設定" : $"lc{n}";
    public static string StyleName(int n) => n < 0 ? "未設定" : $"ls{n}";
}

public sealed class ParseSettingInput
{
    public Guid CustomerId { get; set; }
    public bool Exists { get; set; }
    public Guid ConcurrencyStamp { get; set; }

    public int BeamColor { get; set; } = CustomerParseSetting.Unset;
    public int BeamTextColor { get; set; } = CustomerParseSetting.Unset;
    public int PillarColor { get; set; } = CustomerParseSetting.Unset;
    public int PillarPenStyle { get; set; } = CustomerParseSetting.Unset;
    public ShapeRecognition PillarRecognition { get; set; }
    public int SplitColor { get; set; } = CustomerParseSetting.Unset;
    public int SplitPenStyle { get; set; } = CustomerParseSetting.Unset;
    public ShapeRecognition SplitRecognition { get; set; }

    [Range(0, 10000, ErrorMessage = "0 以上の数値を入力してください。")]
    public double NearSplitMax { get; set; } = 1;

    public int DownPillarColor { get; set; } = CustomerParseSetting.Unset;
    public int BraceColor { get; set; } = CustomerParseSetting.Unset;
    public BraceOrigin BraceOrigin { get; set; }
    public BraceComponent BraceComponent { get; set; }
}

/// <summary>顧客ごとの図面解析設定。第 3 阶段解析图纸时按工事的顧客读取。</summary>
public sealed class CustomerParseSettingService(IAppDbContextFactory dbFactory, ICurrentUser currentUser)
{
    public async Task<ParseSettingInput> GetAsync(Guid customerId, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.View);
        await using var db = await dbFactory.CreateAsync(ct);
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct)) throw new NotFoundException("顧客");
        var s = await db.CustomerParseSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (s is null) return new ParseSettingInput { CustomerId = customerId };
        return new ParseSettingInput
        {
            CustomerId = customerId, Exists = true, ConcurrencyStamp = s.ConcurrencyStamp,
            BeamColor = s.BeamColor, BeamTextColor = s.BeamTextColor,
            PillarColor = s.PillarColor, PillarPenStyle = s.PillarPenStyle, PillarRecognition = s.PillarRecognition,
            SplitColor = s.SplitColor, SplitPenStyle = s.SplitPenStyle, SplitRecognition = s.SplitRecognition,
            NearSplitMax = s.NearSplitMax, DownPillarColor = s.DownPillarColor, BraceColor = s.BraceColor,
            BraceOrigin = s.BraceOrigin, BraceComponent = s.BraceComponent,
        };
    }

    public async Task SaveAsync(ParseSettingInput input, CancellationToken ct = default)
    {
        currentUser.Require(Permissions.Customers.Edit);
        await using var db = await dbFactory.CreateAsync(ct);
        var customer = await db.Customers.Include(c => c.ParseSetting)
                           .FirstOrDefaultAsync(c => c.Id == input.CustomerId, ct) ?? throw new NotFoundException("顧客");

        var s = customer.ParseSetting;
        if (s is null)
        {
            s = new CustomerParseSetting { CustomerId = customer.Id, Customer = customer };
            db.CustomerParseSettings.Add(s);
        }
        else
        {
            db.Db.Entry(s).Property(x => x.ConcurrencyStamp).OriginalValue = input.ConcurrencyStamp;
        }

        s.BeamColor = input.BeamColor;
        s.BeamTextColor = input.BeamTextColor;
        s.PillarColor = input.PillarColor;
        s.PillarPenStyle = input.PillarPenStyle;
        s.PillarRecognition = input.PillarRecognition;
        s.SplitColor = input.SplitColor;
        s.SplitPenStyle = input.SplitPenStyle;
        s.SplitRecognition = input.SplitRecognition;
        s.NearSplitMax = input.NearSplitMax;
        s.DownPillarColor = input.DownPillarColor;
        s.BraceColor = input.BraceColor;
        s.BraceOrigin = input.BraceOrigin;
        s.BraceComponent = input.BraceComponent;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
    }
}
