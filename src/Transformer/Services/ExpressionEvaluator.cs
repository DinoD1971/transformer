using System.Globalization;
using System.Text.Json.Nodes;
using Json.Path;
using Transformer.Exceptions;

namespace Transformer.Services;

public class ExpressionEvaluator : IExpressionEvaluator
{
    public JsonNode? Evaluate(string expression, JsonNode input)
    {
        var parser = new Parser(expression.Trim(), input);
        return parser.ParseComparison();
    }

    private sealed class Parser(string expr, JsonNode input)
    {
        private int _pos;

        // comparison = additive (comp_op additive)?
        public JsonNode? ParseComparison()
        {
            var left = ParseAdditive();

            SkipWhitespace();
            var op = PeekComparisonOp();
            if (op is null)
                return JsonValue.Create(left);

            _pos += op.Length;
            var right = ParseAdditive();
            return JsonValue.Create(Compare(left, op, right));
        }

        // additive = multiplicative (('+' | '-') multiplicative)*
        private decimal ParseAdditive()
        {
            var result = ParseMultiplicative();

            while (true)
            {
                SkipWhitespace();
                if (_pos >= expr.Length) break;
                var ch = expr[_pos];
                if (ch != '+' && ch != '-') break;
                _pos++;
                var rhs = ParseMultiplicative();
                result = ch == '+' ? result + rhs : result - rhs;
            }

            return result;
        }

        // multiplicative = primary (('*' | '/' | '%') primary)*
        private decimal ParseMultiplicative()
        {
            var result = ParsePrimary();

            while (true)
            {
                SkipWhitespace();
                if (_pos >= expr.Length) break;
                var ch = expr[_pos];
                if (ch != '*' && ch != '/' && ch != '%') break;
                _pos++;
                var rhs = ParsePrimary();
                result = ch switch
                {
                    '*' => result * rhs,
                    '/' => rhs == 0 ? throw new TransformationException("Division by zero in expression.") : result / rhs,
                    _   => result % rhs
                };
            }

            return result;
        }

        // primary = '(' additive ')' | jsonpath | numeric_literal
        private decimal ParsePrimary()
        {
            SkipWhitespace();
            if (_pos >= expr.Length) throw new FormatException("Unexpected end of expression.");

            if (expr[_pos] == '(')
            {
                _pos++;
                var val = ParseAdditive();
                SkipWhitespace();
                if (_pos < expr.Length && expr[_pos] == ')') _pos++;
                return val;
            }

            if (expr[_pos] == '$')
                return ResolvePathAsDecimal();

            return ParseNumericLiteral();
        }

        private decimal ResolvePathAsDecimal()
        {
            var start = _pos;
            while (_pos < expr.Length && !IsDelimiter(expr[_pos]))
                _pos++;
            var path = expr[start.._pos].Trim();

            var node = ResolvePath(path);
            if (node is null)
                throw new TransformationException($"Expression operand '{path}' resolved to null or absent value.");

            return ToDecimal(node, path);
        }

        private decimal ParseNumericLiteral()
        {
            var start = _pos;
            if (_pos < expr.Length && expr[_pos] == '-') _pos++;
            while (_pos < expr.Length && (char.IsDigit(expr[_pos]) || expr[_pos] == '.'))
                _pos++;
            var token = expr[start.._pos];
            if (!decimal.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                throw new FormatException($"Cannot parse numeric literal '{token}'.");
            return d;
        }

        private string? PeekComparisonOp()
        {
            foreach (var op in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                if (_pos + op.Length <= expr.Length &&
                    expr.Substring(_pos, op.Length) == op)
                    return op;
            }
            return null;
        }

        // stops path consumption at operators, spaces, and parens
        private static bool IsDelimiter(char c) => c is ' ' or '+' or '-' or '*' or '/' or '%' or '=' or '!' or '<' or '>' or '(' or ')';

        private JsonNode? ResolvePath(string path)
        {
            var jsonPath = JsonPath.Parse(path);
            return jsonPath.Evaluate(input).Matches.FirstOrDefault()?.Value;
        }

        private static decimal ToDecimal(JsonNode node, string path)
        {
            if (node is not JsonValue jv)
                throw new TransformationException($"Expression operand '{path}' is not a numeric value.");

            if (jv.TryGetValue<decimal>(out var d)) return d;
            if (jv.TryGetValue<double>(out var dbl)) return (decimal)dbl;
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<string>(out var s) &&
                decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var sd)) return sd;

            throw new TransformationException($"Expression operand '{path}' cannot be converted to a number.");
        }

        private static bool Compare(decimal left, string op, decimal right) =>
            op switch
            {
                "==" => left == right,
                "!=" => left != right,
                ">"  => left > right,
                "<"  => left < right,
                ">=" => left >= right,
                "<=" => left <= right,
                _    => false
            };

        private void SkipWhitespace()
        {
            while (_pos < expr.Length && expr[_pos] == ' ')
                _pos++;
        }
    }
}
