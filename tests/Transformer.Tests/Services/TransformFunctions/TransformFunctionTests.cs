using System.Text.Json;
using System.Text.Json.Nodes;
using Transformer.Exceptions;
using Transformer.Services.TransformFunctions;

namespace Transformer.Tests.Services.TransformFunctions;

public class TransformFunctionTests
{
    private static JsonElement Params(string json) => JsonDocument.Parse(json).RootElement;

    // --- trim ---

    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespace()
    {
        var result = new TrimTransformFunction().Execute(JsonValue.Create("  hello  "), null);
        Assert.Equal("hello", result!.GetValue<string>());
    }

    [Fact]
    public void Trim_NonStringInput_ThrowsTransformationException()
    {
        Assert.Throws<TransformationException>(() =>
            new TrimTransformFunction().Execute(JsonValue.Create(42), null));
    }

    // --- round ---

    [Fact]
    public void Round_Precision2_RoundsCorrectly()
    {
        var result = new RoundTransformFunction().Execute(
            JsonValue.Create(3.14159), Params("""{"precision":2}"""));
        Assert.Equal(3.14m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Round_Precision0_RoundsToInteger()
    {
        var result = new RoundTransformFunction().Execute(
            JsonValue.Create(2.7), Params("""{"precision":0}"""));
        Assert.Equal(3m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Round_NoPrecisionParameter_DefaultsToZeroDecimals()
    {
        var result = new RoundTransformFunction().Execute(JsonValue.Create(4.5), null);
        Assert.Equal(5m, result!.GetValue<decimal>());
    }

    [Fact]
    public void Round_NonNumericInput_ThrowsTransformationException()
    {
        Assert.Throws<TransformationException>(() =>
            new RoundTransformFunction().Execute(JsonValue.Create("abc"), Params("""{"precision":2}""")));
    }

    // --- contains ---

    [Fact]
    public void Contains_ArrayContainsValue_ReturnsTrue()
    {
        var array = JsonNode.Parse("""["VIP","Gold","Silver"]""");
        var result = new ContainsTransformFunction().Execute(array, Params("""{"value":"VIP"}"""));
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void Contains_ArrayDoesNotContainValue_ReturnsFalse()
    {
        var array = JsonNode.Parse("""["Gold","Silver"]""");
        var result = new ContainsTransformFunction().Execute(array, Params("""{"value":"VIP"}"""));
        Assert.False(result!.GetValue<bool>());
    }

    [Fact]
    public void Contains_NullInput_ReturnsFalse()
    {
        var result = new ContainsTransformFunction().Execute(null, Params("""{"value":"VIP"}"""));
        Assert.False(result!.GetValue<bool>());
    }

    [Fact]
    public void Contains_NonArrayInput_ThrowsTransformationException()
    {
        Assert.Throws<TransformationException>(() =>
            new ContainsTransformFunction().Execute(JsonValue.Create("not-array"), Params("""{"value":"VIP"}""")));
    }

    // --- now ---

    [Fact]
    public void Now_ReturnsIso8601StringByDefault()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = new NowTransformFunction().Execute(null, null);
        var after = DateTime.UtcNow.AddSeconds(1);

        var str = result!.GetValue<string>();
        Assert.True(DateTime.TryParse(str, out var parsed));
        Assert.InRange(parsed.ToUniversalTime(), before, after);
    }

    [Fact]
    public void Now_IgnoresSourceInput()
    {
        var result1 = new NowTransformFunction().Execute(null, null);
        var result2 = new NowTransformFunction().Execute(JsonValue.Create("ignored"), null);

        Assert.NotNull(result1!.GetValue<string>());
        Assert.NotNull(result2!.GetValue<string>());
    }

    [Fact]
    public void Now_UsesDateFormatWhenProvided()
    {
        var result = new NowTransformFunction().Execute(null, null, "yyyy-MM-dd");
        var str = result!.GetValue<string>();
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", str);
    }
}
