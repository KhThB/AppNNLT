using QRCoder;

namespace TourGuide.WebAdmin.Helpers
{
    public static class QRCodeHelper
    {
        public static string GenerateQRCode(string url)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                // Tạo dữ liệu QR với mức độ sửa lỗi Q (25%)
                using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

                // Sử dụng PngByteQRCode để xuất ra mảng byte ảnh PNG trực tiếp
                using var qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);

                // Chuyển sang chuỗi Base64 để thẻ <img> hiển thị được
                return $"data:image/png;base64,{Convert.ToBase64String(qrCodeAsPngByteArr)}";
            }
            catch (Exception ex)
            {
                // In lỗi ra màn hình Debug để bạn kiểm tra
                System.Diagnostics.Debug.WriteLine($"LỖI TRONG HELPER: {ex.Message}");
                return "";
            }
        }
    }
}