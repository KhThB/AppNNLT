using MudBlazor;

namespace TourGuide.WebAdmin.Helpers
{
    public static class CustomTheme
    {
        public static MudTheme MerchantTheme = new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#1E3A8A", // Deep Dark Blue (Tailwind blue-900)
                Secondary = "#D97706", // Amber/Gold for contrast
                AppbarBackground = "#0F172A", // Slate 900
                AppbarText = "#FFFFFF",
                Background = "#F8FAFC", // Slate 50
                DrawerBackground = "#FFFFFF",
                Surface = "#FFFFFF",
                TextPrimary = "#1E293B", // Slate 800
                TextSecondary = "#64748B", // Slate 500
                ActionDefault = "#1E3A8A",
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400", // Đã thêm ngoặc kép thành chuỗi (string)
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                    FontWeight = "700", // Đã thêm ngoặc kép
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                    FontWeight = "600", // Đã thêm ngoặc kép
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                    FontWeight = "600", // Đã thêm ngoặc kép
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontWeight = "500", // Đã thêm ngoặc kép
                }
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px", // Bo góc mềm mại, hiện đại
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "300px"
            }
        };
    }
}