using System.Text.Json.Nodes;

namespace Transformer.Services;

public interface IConditionEvaluator
{
    bool Evaluate(string expression, JsonNode input);
}
