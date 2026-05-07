using System.Text.Json;
using System.Text.Json.Nodes;

namespace Transformer.Services.TransformFunctions;

public interface ITransformFunction
{
    JsonNode? Execute(JsonNode? input, JsonElement? parameters, string? dateFormat = null);
}
