using System.Text.Json;
using System.Text.Json.Serialization;

namespace PinkRooster.Mcp.Responses;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Indented = new(JsonSerializerOptions.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
