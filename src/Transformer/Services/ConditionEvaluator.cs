using System.Text.Json.Nodes;
using Json.Path;
using Microsoft.Extensions.Logging;

namespace Transformer.Services;

public class ConditionEvaluator : IConditionEvaluator
{
    private static readonly string[] TwoTokenOps = ["!= null", "== null"];
    private static readonly string[] Operators = ["!=", "==", ">=", "<=", ">", "<"];

    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(ILogger<ConditionEvaluator> logger)
    {
        _logger = logger;
    }

    public bool Evaluate(string expression, JsonNode input)
    {
        try
        {
            return EvaluateCore(expression.Trim(), input);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Condition expression '{Expression}' could not be evaluated: {Message} — treating as false.", expression, ex.Message);
            return false;
        }
    }

    private bool EvaluateCore(string expr, JsonNode input)
    {
        // Check two-token RHS first (e.g. "!= null") to avoid splitting on the operator inside it
        foreach (var twoToken in TwoTokenOps)
        {
            var idx = expr.IndexOf(" " + twoToken, StringComparison.Ordinal);
            if (idx < 0) continue;

            var lhsPath = expr[..idx].Trim();
            var lhsValue = ResolvePath(lhsPath, input);
            var op = twoToken[..2]; // "==" or "!="
            return op == "==" ? lhsValue is null : lhsValue is not null;
        }

        foreach (var op in Operators)
        {
            var opIdx = expr.IndexOf(" " + op + " ", StringComparison.Ordinal);
            if (opIdx < 0) continue;

            var lhsPath = expr[..opIdx].Trim();
            var rhsToken = expr[(opIdx + op.Length + 2)..].Trim();

            var lhsValue = ResolvePath(lhsPath, input);
            var rhsValue = ParseLiteral(rhsToken);

            return Compare(lhsValue, op, rhsValue);
        }

        throw new FormatException($"Unrecognised condition expression: '{expr}'");
    }

    private static JsonNode? ResolvePath(string path, JsonNode input)
    {
        var jsonPath = JsonPath.Parse(path);
        var result = jsonPath.Evaluate(input);
        return result.Matches.FirstOrDefault()?.Value;
    }

    private static JsonNode? ParseLiteral(string token)
    {
        if (token.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (token.Equals("true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (token.Equals("false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);

        if (token.StartsWith('"') && token.EndsWith('"') && token.Length >= 2)
            return JsonValue.Create(token[1..^1]);

        if (decimal.TryParse(token, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return JsonValue.Create(d);

        throw new FormatException($"Cannot parse RHS literal: '{token}'");
    }

    private static bool Compare(JsonNode? lhs, string op, JsonNode? rhs)
    {
        if (lhs is null && rhs is null) return op is "==" or ">=" or "<=";
        if (lhs is null || rhs is null) return op == "!=";

        // Numeric comparison
        if (TryGetDecimal(lhs, out var lhsD) && TryGetDecimal(rhs, out var rhsD))
        {
            return op switch
            {
                "==" => lhsD == rhsD,
                "!=" => lhsD != rhsD,
                ">"  => lhsD > rhsD,
                "<"  => lhsD < rhsD,
                ">=" => lhsD >= rhsD,
                "<=" => lhsD <= rhsD,
                _    => false
            };
        }

        // String / boolean comparison (equality only for non-numeric)
        var lhsStr = lhs.ToJsonString().Trim('"');
        var rhsStr = rhs.ToJsonString().Trim('"');
        return op switch
        {
            "==" => string.Equals(lhsStr, rhsStr, StringComparison.Ordinal),
            "!=" => !string.Equals(lhsStr, rhsStr, StringComparison.Ordinal),
            _    => false
        };
    }

    private static bool TryGetDecimal(JsonNode node, out decimal value)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<decimal>(out value)) return true;
            if (jv.TryGetValue<double>(out var d)) { value = (decimal)d; return true; }
            if (jv.TryGetValue<long>(out var l)) { value = l; return true; }
            if (jv.TryGetValue<int>(out var i)) { value = i; return true; }
        }
        value = 0;
        return false;
    }
}
