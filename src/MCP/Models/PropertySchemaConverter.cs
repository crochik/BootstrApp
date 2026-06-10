using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace McpServer.Models;

/// <summary>
/// Custom JSON writer for <see cref="PropertySchema"/>: when <c>RawSchema</c> is set
/// (an explicit schema registered via <c>McpSchemaOverrides</c>), emit it verbatim;
/// otherwise emit the typed fields in the same shape the default attribute-based
/// serializer would produce. PropertySchema is write-only — Read is unsupported.
/// </summary>
internal sealed class PropertySchemaConverter : JsonConverter<PropertySchema>
{
    public override PropertySchema Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("PropertySchema deserialization is not supported.");

    public override void Write(
        Utf8JsonWriter writer, PropertySchema value, JsonSerializerOptions options)
    {
        if (value.RawSchema is JsonNode raw)
        {
            raw.WriteTo(writer, options);
            return;
        }

        writer.WriteStartObject();

        writer.WriteString("type", value.Type ?? string.Empty);
        writer.WriteString("description", value.Description ?? string.Empty);

        if (value.Format != null)
            writer.WriteString("format", value.Format);

        if (value.Properties != null)
        {
            writer.WritePropertyName("properties");
            JsonSerializer.Serialize(writer, value.Properties, options);
        }

        if (value.Required != null)
        {
            writer.WritePropertyName("required");
            JsonSerializer.Serialize(writer, value.Required, options);
        }

        if (value.Items != null)
        {
            writer.WritePropertyName("items");
            JsonSerializer.Serialize(writer, value.Items, options);
        }

        if (value.Enum != null)
        {
            writer.WritePropertyName("enum");
            JsonSerializer.Serialize(writer, value.Enum, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// Custom JSON writer for <see cref="ToolInputSchema"/>. Mirrors
/// <see cref="PropertySchemaConverter"/> so that a top-level overridden type
/// (e.g. an output schema whose CLR root type has a registered override)
/// can be emitted verbatim.
/// </summary>
internal sealed class ToolInputSchemaConverter : JsonConverter<ToolInputSchema>
{
    public override ToolInputSchema Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("ToolInputSchema deserialization is not supported.");

    public override void Write(
        Utf8JsonWriter writer, ToolInputSchema value, JsonSerializerOptions options)
    {
        if (value.RawSchema is JsonNode raw)
        {
            raw.WriteTo(writer, options);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("type", value.Type ?? "object");

        writer.WritePropertyName("properties");
        JsonSerializer.Serialize(writer, value.Properties ?? new(), options);

        writer.WritePropertyName("required");
        JsonSerializer.Serialize(writer, value.Required ?? new(), options);

        writer.WriteEndObject();
    }
}
