using System.Globalization;
using Jw.Application;
using Jw.Domain.Identity;
using Jw.Infrastructure;
using Jw.Infrastructure.Identity;
using Jw.Infrastructure.Persistence;
using Jw.Infrastructure.Seeding;
using Jw.Web.Auth;
using Jw.Web.Components;
using Jw.Web.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ---- 业务层与数据 ----
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("未配置连接串 ConnectionStrings:Default");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(o => o.UseSqlServer(connectionString));

// ---- 认证与授权 ----
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/access-denied";
    o.Cookie.Name = "jw.auth";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
});
// 登录 Cookie 的加密密钥。IIS 应用池回收、重新部署后密钥不能变，否则所有人都会被登出。
// 生产环境配置 DataProtection:KeysPath（站点目录之外、应用池账号可写）。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("NewJwWeb");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi(); // 应用池账号的 DPAPI（需加载用户配置文件）
}
// 每次 HTTP 请求最多每分钟校验一次安全戳：被停用或权限变更后，旧登录会话会失效
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddIdentityCore<AppUser>(o =>
    {
        o.SignIn.RequireConfirmedAccount = true; // 交给 ActiveUserConfirmation：停用的用户不能登录
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = false;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<JwDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddErrorDescriber<JapaneseIdentityErrorDescriber>();
builder.Services.AddScoped<IUserConfirmation<AppUser>, ActiveUserConfirmation>();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();
builder.Services.AddScoped<CircuitHandler, UserCircuitHandler>();

// ---- 界面 ----
builder.Services.AddLocalization();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices(c =>
{
    c.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    c.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddScoped<UiRunner>();

TimeDisplay.Configure(builder.Configuration["App:TimeZone"]);

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    // 上传的图纸放在站点目录里的话，重新部署（覆盖或清空发布目录）时会丢失
    if (!Path.IsPathRooted(app.Configuration["Storage:Root"] ?? ""))
        app.Logger.LogWarning("Storage:Root が相対パスです（{Root}）。再デプロイで図面ファイルが消えないよう、サイト外の絶対パスを設定してください。",
            app.Configuration["Storage:Root"] ?? "App_Data/files");
    if (string.IsNullOrEmpty(app.Configuration["DataProtection:KeysPath"]))
        app.Logger.LogWarning("DataProtection:KeysPath が未設定です。IIS ではアプリケーションプールの「ユーザープロファイルの読み込み」を有効にしないと、再起動のたびに全員ログアウトされます。");
}

var culture = new CultureInfo(app.Configuration["App:Culture"] ?? "ja-JP");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture],
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAccountEndpoints();
app.MapFileEndpoints();

app.Run();

public partial class Program;
