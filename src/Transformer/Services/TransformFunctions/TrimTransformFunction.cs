using System.Text.Json;
using System.Text.Json.Nodes;
using Transformer.Exceptions;

namespace Transformer.Services.TransformFunctions;

public class TrimTransformFunction : ITransformFunction
{
    public JsonNode? Execute(JsonNode? input, JsonElement? parameters, string? dateFormat = null)
    {
        if (input is null)
            return null;

        if (input is not JsonValue val || !val.TryGetValue<string>(out var str))
            throw new TransformationException($"'trim' requires a string input but received: {input.ToJsonString()}");

        return JsonValue.Create(str.Trim());
    }
}
