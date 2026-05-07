using System.Text.Json;
using System.Text.Json.Nodes;
using Transformer.Exceptions;

namespace Transformer.Services.TransformFunctions;

public class ContainsTransformFunction : ITransformFunction
{
    public JsonNode? Execute(JsonNode? input, JsonElement? parameters, string? dateFormat = null)
    {
        if (input is null)
            return JsonValue.Create(false);

        if (input is not JsonArray array)
            throw new TransformationException($"'contains' requires an array input but received: {input.ToJsonString()}");

        if (!parameters.HasValue || parameters.Value.ValueKind == JsonValueKind.Undefined)
            throw new TransformationException("'contains' requires a 'value' parameter.");

        JsonElement searchEl = parameters.Value.ValueKind == JsonValueKind.Object &&
                               parameters.Value.TryGetProperty("value", out var prop)
            ? prop
            : parameters.Value;

        var searchJson = searchEl.GetRawText();

        foreach (var item in array)
        {
            if (item?.ToJsonString() == searchJson)
                return JsonValue.Create(true);
        }

        return JsonValue.Create(false);
    }
}
