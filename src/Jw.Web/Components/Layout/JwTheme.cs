using MudBlazor;

namespace Jw.Web.Components.Layout;

/// <summary>主题：白底、蓝色强调（沿用旧版的蓝色），系统日文字体。</summary>
public static class JwTheme
{
    private static readonly string[] Fonts = ["Meiryo UI", "Yu Gothic UI", "Hiragino Sans", "Noto Sans JP", "Segoe UI", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1d6fd8",
            PrimaryDarken = "#1557ab",
            Secondary = "#5f6b7a",
            Tertiary = "#0f9d8a",
            Info = "#1d6fd8",
            Success = "#1a9d5a",
            Warning = "#e08a00",
            Error = "#d93b3b",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2937",
            Background = "#f5f7fa",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#374151",
            DrawerIcon = "#6b7280",
            TextPrimary = "#1f2937",
            TextSecondary = "#6b7280",
            LinesDefault = "#e5e7eb",
            TableLines = "#eef0f3",
            Divider = "#e5e7eb",
            ActionDefault = "#6b7280",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Fonts, FontSize = ".875rem" },
            H5 = new H5Typography { FontFamily = Fonts, FontSize = "1.35rem", FontWeight = "700" },
            H6 = new H6Typography { FontFamily = Fonts, FontSize = "1.05rem", FontWeight = "700" },
            Button = new ButtonTypography { FontFamily = Fonts, TextTransform = "none", FontWeight = "500" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "232px",
            AppbarHeight = "56px",
        },
    };
}
