using MudBlazor;

namespace TourGuide.WebAdmin.Helpers;

public static class CustomTheme
{
    public static MudTheme MerchantTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#ea580c",
            Secondary = "#f97316",
            Tertiary = "#fb923c",
            AppbarBackground = "#c2410c",
            AppbarText = "#fff7ed",
            Background = "#fff7ed",
            DrawerBackground = "#9a3412",
            Surface = "#ffffff",
            TextPrimary = "#1f2937",
            TextSecondary = "#6b7280",
            ActionDefault = "#ea580c",
            Info = "#0284c7",
            Success = "#16a34a",
            Warning = "#f59e0b",
            Error = "#dc2626",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                FontSize = "1rem",
                FontWeight = "400",
            },
            H4 = new H4Typography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                FontWeight = "750",
            },
            H5 = new H5Typography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                FontWeight = "700",
            },
            H6 = new H6Typography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                FontWeight = "650",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontWeight = "550",
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "300px",
        },
    };
}
