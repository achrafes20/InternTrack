namespace InternTrack.Application.Common.Interfaces;

/// <summary>
/// Les dates sont stockées en UTC ; elles sont présentées aux personnes (e-mails, PDF) dans le fuseau
/// de l'établissement (Africa/Casablanca par défaut, qui gère le passage UTC+1 / UTC+0 du ramadan).
/// </summary>
public interface ITimeZoneService
{
    string TimeZoneId { get; }

    DateTime ToLocal(DateTime utc);

    /// <summary>Date du jour dans le fuseau de l'établissement.</summary>
    DateTime LocalToday { get; }

    /// <summary>Ex. « 25/11/2026 à 14:30 (heure du Maroc) ».</summary>
    string FormatDateTime(DateTime utc);
}
