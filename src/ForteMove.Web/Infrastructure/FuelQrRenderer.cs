using QRCoder;
namespace ForteMove.Web.Infrastructure
{
    public static class FuelQrRenderer
    {
        public static byte[] Render(string token)
        {
            using(var generator=new QRCodeGenerator())
            using(var data=generator.CreateQrCode(token,QRCodeGenerator.ECCLevel.Q))
            using(var image=new PngByteQRCode(data)) return image.GetGraphic(6);
        }
    }
}
