using System.Text.Json.Nodes;
using Transformer.Exceptions;
using Transformer.Services;

namespace Transformer.Tests.Services;

public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _eval = new();

    private static JsonNode Input(string json) => JsonNode.Parse(json)!;

    // --- arithmetic ---

    [Fact]
    public void Evaluate_Addition_ReturnsSum()
    {
        var result = _eval.Evaluate("$.a + $.b", Input("""{"a":3,"b":4}"""));
        Assert.Equal(7m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_Subtraction_ReturnsDifference()
    {
        var result = _eval.Evaluate("$.a - $.b", Input("""{"a":10,"b":3}"""));
        Assert.Equal(7m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_Multiplication_ReturnsProduct()
    {
        var result = _eval.Evaluate("$.qty * $.price", Input("""{"qty":3,"price":9.99}"""));
        Assert.Equal(29.97m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_Division_ReturnsQuotient()
    {
        var result = _eval.Evaluate("$.total / $.count", Input("""{"total":10,"count":4}"""));
        Assert.Equal(2.5m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_Modulo_ReturnsRemainder()
    {
        var result = _eval.Evaluate("$.a % $.b", Input("""{"a":10,"b":3}"""));
        Assert.Equal(1m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_PrecedenceMultiplicationBeforeAddition()
    {
        var result = _eval.Evaluate("$.a + $.b * $.c", Input("""{"a":2,"b":3,"c":4}"""));
        Assert.Equal(14m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_Parentheses_OverridePrecedence()
    {
        var result = _eval.Evaluate("($.a + $.b) * $.c", Input("""{"a":2,"b":3,"c":4}"""));
        Assert.Equal(20m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Evaluate_LiteralOperand_UsedDirectly()
    {
        var result = _eval.Evaluate("$.price * 1.1", Input("""{"price":100}"""));
        Assert.Equal(110m, result!.GetValue<decimal>());
    }

    // --- comparison ---

    [Fact]
    public void Evaluate_GreaterThan_True_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.total > 1000", Input("""{"total":1500}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_GreaterThan_False_ReturnsFalse()
    {
        var result = _eval.Evaluate("$.total > 1000", Input("""{"total":500}"""));
        Assert.False(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_LessThan_True_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.count < 10", Input("""{"count":5}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_GreaterThanOrEqual_Equal_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.total >= 100", Input("""{"total":100}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_LessThanOrEqual_Equal_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.total <= 100", Input("""{"total":100}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_Equal_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.status == 1", Input("""{"status":1}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Evaluate_NotEqual_ReturnsTrue()
    {
        var result = _eval.Evaluate("$.status != 0", Input("""{"status":1}"""));
        Assert.True(result!.GetValue<bool>());
    }

    // --- mixed ---

    [Fact]
    public void Evaluate_ArithmeticThenComparison()
    {
        var result = _eval.Evaluate("$.qty * $.price > 100", Input("""{"qty":5,"price":25}"""));
        Assert.True(result!.GetValue<bool>());
    }

    // --- missing operand ---

    [Fact]
    public void Evaluate_MissingOperand_ThrowsTransformationException()
    {
        Assert.Throws<TransformationException>(() =>
            _eval.Evaluate("$.missing * 2", Input("""{"other":1}""")));
    }
}
