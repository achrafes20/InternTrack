using System.Globalization;
using Microsoft.Extensions.Configuration;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Infrastructure.Services.Time;

public class TimeZoneService : ITimeZoneService
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _timeProvider;

    public TimeZoneService(IConfiguration configuration, TimeProvider? timeProvider = null)
    {
        TimeZoneId = configuration["AppConfig:TimeZone"] ?? "Africa/Casablanca";
        // Identifiant IANA : résolu nativement sous Linux (tzdata) et sous Windows (.NET 6+ avec ICU).
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string TimeZoneId { get; }

    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);

    public DateTime LocalToday => ToLocal(_timeProvider.GetUtcNow().UtcDateTime).Date;

    public string FormatDateTime(DateTime utc)
        => ToLocal(utc).ToString("dd/MM/yyyy 'à' HH:mm", French) + " (heure du Maroc)";
}
