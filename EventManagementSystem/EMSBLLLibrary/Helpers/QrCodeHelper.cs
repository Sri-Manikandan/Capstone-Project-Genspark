using QRCoder;

namespace EMSBLLLibrary.Helpers
{
    public static class QrCodeHelper
    {
        // Renders payload as a scannable QR PNG, returned as a base64 string.
        public static string GeneratePngBase64(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(data);
            var bytes = pngQrCode.GetGraphic(20);
            return Convert.ToBase64String(bytes);
        }
    }
}
