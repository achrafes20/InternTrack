namespace InternTrack.Application.Common.Interfaces;

public interface IQrCodeService
{
    byte[] GenerateQrCodePng(string payloadUrl);
}

