using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Services;

namespace Transformer.Tests.Services;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator =
        new(new Mock<ILogger<ConditionEvaluator>>().Object);

    private static JsonNode Input(string json) => JsonNode.Parse(json)!;

    // --- null checks ---

    [Fact]
    public void Evaluate_NotNull_ValuePresent_ReturnsTrue()
    {
        Assert.True(_evaluator.Evaluate("$.x != null", Input("""{"x":42}""")));
    }

    [Fact]
    public void Evaluate_NotNull_ValueAbsent_ReturnsFalse()
    {
        Assert.False(_evaluator.Evaluate("$.x != null", Input("""{}""")));
    }

    [Fact]
    public void Evaluate_EqualNull_ValueNull_ReturnsTrue()
    {
        Assert.True(_evaluator.Evaluate("$.x == null", Input("""{"x":null}""")));
    }

    [Fact]
    public void Evaluate_EqualNull_ValuePresent_ReturnsFalse()
    {
        Assert.False(_evaluator.Evaluate("$.x == null", Input("""{"x":1}""")));
    }

    // --- numeric operators ---

    [Fact]
    public void Evaluate_GreaterThan_True() =>
        Assert.True(_evaluator.Evaluate("$.total > 1000", Input("""{"total":1500}""")));

    [Fact]
    public void Evaluate_GreaterThan_False() =>
        Assert.False(_evaluator.Evaluate("$.total > 1000", Input("""{"total":500}""")));

    [Fact]
    public void Evaluate_LessThan_True() =>
        Assert.True(_evaluator.Evaluate("$.total < 100", Input("""{"total":50}""")));

    [Fact]
    public void Evaluate_GreaterThanOrEqual_Equal_ReturnsTrue() =>
        Assert.True(_evaluator.Evaluate("$.total >= 100", Input("""{"total":100}""")));

    [Fact]
    public void Evaluate_LessThanOrEqual_Equal_ReturnsTrue() =>
        Assert.True(_evaluator.Evaluate("$.total <= 100", Input("""{"total":100}""")));

    // --- equality ---

    [Fact]
    public void Evaluate_EqualString_Match_ReturnsTrue() =>
        Assert.True(_evaluator.Evaluate("""$.status == "paid" """, Input("""{"status":"paid"}""")));

    [Fact]
    public void Evaluate_EqualString_NoMatch_ReturnsFalse() =>
        Assert.False(_evaluator.Evaluate("""$.status == "paid" """, Input("""{"status":"pending"}""")));

    [Fact]
    public void Evaluate_NotEqualString_ReturnsTrue() =>
        Assert.True(_evaluator.Evaluate("""$.status != "paid" """, Input("""{"status":"pending"}""")));

    // --- nested path ---

    [Fact]
    public void Evaluate_NestedPath_ReturnsTrue() =>
        Assert.True(_evaluator.Evaluate("$.order.total > 500", Input("""{"order":{"total":1000}}""")));

    // --- unevaluable expression returns false ---

    [Fact]
    public void Evaluate_InvalidExpression_ReturnsFalse()
    {
        var result = _evaluator.Evaluate("not a valid expression", Input("{}"));
        Assert.False(result);
    }
}
