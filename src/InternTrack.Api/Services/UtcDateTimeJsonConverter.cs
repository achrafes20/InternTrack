using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternTrack.Api.Services;

/// <summary>
/// Toutes les dates échangées par l'API sont en UTC. Une date reçue sans fuseau (« 2026-09-01 » ou
/// « 2026-09-01T10:00:00 ») est interprétée comme UTC au lieu de provoquer une erreur 500 à l'écriture
/// en base (PostgreSQL « timestamp with time zone » n'accepte que des DateTime de type UTC) ; une date
/// avec décalage est convertie en UTC. En sortie, les dates portent toujours le suffixe « Z ».
/// S'applique aussi aux DateTime? (System.Text.Json réutilise le convertisseur du type sous-jacent).
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        writer.WriteStringValue(utc);
    }
}
