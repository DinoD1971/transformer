using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Transformer.Services.TransformFunctions;

public class NowTransformFunction : ITransformFunction
{
    public JsonNode? Execute(JsonNode? input, JsonElement? parameters, string? dateFormat = null)
    {
        var now = DateTime.UtcNow;
        var formatted = string.IsNullOrEmpty(dateFormat)
            ? now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : now.ToString(dateFormat, CultureInfo.InvariantCulture);
        return JsonValue.Create(formatted);
    }
}
