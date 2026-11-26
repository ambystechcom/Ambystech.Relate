using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ambystech.Relate.Samples.GraphCursor;

/// <summary>Compact JSON for tool results: the agent's context is the scarce resource.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
