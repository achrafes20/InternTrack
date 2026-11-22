using QRCoder;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Infrastructure.Services.Qr;

public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrCodePng(string payloadUrl)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payloadUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);
    }
}

