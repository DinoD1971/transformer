using System.Text.Json;
using System.Text.Json.Nodes;
using Transformer.Exceptions;

namespace Transformer.Services.TransformFunctions;

public class RoundTransformFunction : ITransformFunction
{
    public JsonNode? Execute(JsonNode? input, JsonElement? parameters, string? dateFormat = null)
    {
        if (input is null)
            return null;

        if (!TryGetDecimal(input, out var value))
            throw new TransformationException($"'round' requires a numeric input but received: {input.ToJsonString()}");

        int precision = 0;
        if (parameters.HasValue &&
            parameters.Value.ValueKind == JsonValueKind.Object &&
            parameters.Value.TryGetProperty("precision", out var precisionEl))
        {
            precision = precisionEl.GetInt32();
        }

        return JsonValue.Create(Math.Round(value, precision, MidpointRounding.AwayFromZero));
    }

    private static bool TryGetDecimal(JsonNode node, out decimal value)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue<decimal>(out value)) return true;
            if (val.TryGetValue<double>(out var d)) { value = (decimal)d; return true; }
            if (val.TryGetValue<long>(out var l)) { value = l; return true; }
            if (val.TryGetValue<int>(out var i)) { value = i; return true; }
        }
        value = 0;
        return false;
    }
}
