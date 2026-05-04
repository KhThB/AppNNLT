using System.Globalization;

namespace Mobile.Converters;

public class DistanceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double distance)
        {
            if (distance < 1000)
            {
                // Nếu dưới 1000m, hiện nguyên số mét không thập phân
                return $"{Math.Round(distance)}m";
            }
            else
            {
                // Nếu trên 1000m, chuyển sang km và lấy 1 chữ số thập phân
                double kilometers = distance / 1000.0;
                return $"{kilometers:0.0}km";
            }
        }
        
        return "0m"; // Giá trị mặc định nếu null hoặc không phải là double
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Không dùng cho Binding 2 chiều
        throw new NotImplementedException();
    }
}
