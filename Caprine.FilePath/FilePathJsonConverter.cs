using System.Text.Json;
using System.Text.Json.Serialization;

namespace Caprine.FilePath;

/// <summary>Serializes native paths as normalized JSON strings, including dictionary keys.</summary>
/// <remarks>Deserialization uses the current operating system's path rules and performs no filesystem access.</remarks>
public sealed class FilePathJsonConverter : JsonConverter<FilePath>
{
    /// <inheritdoc />
    public override FilePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Parse(reader.GetString());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FilePath value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);

    /// <inheritdoc />
    public override FilePath ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Parse(reader.GetString());

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, FilePath value, JsonSerializerOptions options) => writer.WritePropertyName(value.Value);

    private static FilePath Parse(string? value)
    {
        if (!FilePath.TryParse(value, out var result))
        {
            throw new JsonException("A path must be a string without null characters.");
        }

        return result;
    }
}
