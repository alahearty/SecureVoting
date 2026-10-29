using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureVoting.Domain;

namespace SecureVoting.Application.Security;

public static class AuditHash
{
    public static string Compute(AuditLog entry) =>
        Compute(
            entry.Sequence,
            entry.EventId,
            entry.OccurredAtUtc,
            entry.ActorUserId,
            entry.ActorDisplay,
            entry.EventType,
            entry.EntityType,
            entry.EntityId,
            entry.Outcome,
            entry.CorrelationId,
            entry.DetailsJson,
            entry.PreviousHash);

    public static string Compute(
        long sequence,
        Guid eventId,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string actorDisplay,
        string eventType,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        string? correlationId,
        string detailsJson,
        string previousHash)
    {
        var canonicalDetails = CanonicalJson.Normalize(detailsJson);
        var payload = string.Join(
            '\u001f',
            sequence.ToString(CultureInfo.InvariantCulture),
            eventId.ToString("D"),
            occurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            actorUserId?.ToString("D") ?? string.Empty,
            actorDisplay,
            eventType,
            entityType,
            entityId ?? string.Empty,
            outcome.ToString(),
            correlationId ?? string.Empty,
            canonicalDetails,
            previousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

}

public static class CanonicalJson
{
    public static string Normalize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteElement(writer, document.RootElement);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
