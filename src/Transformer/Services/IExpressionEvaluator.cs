using System.Text.Json.Nodes;

namespace Transformer.Services;

public interface IExpressionEvaluator
{
    JsonNode? Evaluate(string expression, JsonNode input);
}
