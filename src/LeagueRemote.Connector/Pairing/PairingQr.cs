using QRCoder;

namespace LeagueRemote.Connector.Pairing;

/// <summary>Renders the pairing deep link as a PNG QR code for the tray's QR window.</summary>
internal static class PairingQr
{
    public static byte[] ToPng(string link, int pixelsPerModule = 8)
    {
        using var data = QRCodeGenerator.GenerateQrCode(link, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
