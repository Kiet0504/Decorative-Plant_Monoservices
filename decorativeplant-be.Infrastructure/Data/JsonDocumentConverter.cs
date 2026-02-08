using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace decorativeplant_be.Infrastructure.Data;

/// <summary>
/// EF Core value converter for JsonDocument to/from jsonb. Use with .HasConversion(JsonDocumentConverter.Instance).
/// </summary>
public static class JsonDocumentConverter
{
    public static readonly ValueConverter<JsonDocument?, string?> Instance = new(
        v => v == null ? null : JsonSerializer.Serialize(v.RootElement),
        v => string.IsNullOrEmpty(v) ? null : JsonDocument.Parse(v));
}
