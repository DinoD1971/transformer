using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Models;
using Transformer.Services;

namespace Transformer.Tests.Services;

public class TransformationEngineTests
{
    private readonly TransformationEngine _engine;

    public TransformationEngineTests()
    {
        var logger = new Mock<ILogger<TransformationEngine>>();
        _engine = new TransformationEngine(logger.Object);
    }

    private static JsonObject ParseInput(string json) =>
        JsonNode.Parse(json)!.AsObject();

    private static TransformConfig Config(params (string source, string target)[] mappings) =>
        new()
        {
            Mappings = mappings.Select(m => new MappingConfig { Source = m.source, Target = m.target }).ToList()
        };

    [Fact]
    public void Transform_SimpleTopLevelMapping_MapsValue()
    {
        var input = ParseInput("""{"id":"order-1"}""");
        var config = Config(("$.id", "orderId"));

        var result = _engine.Transform(input, config);

        Assert.Equal("order-1", result["orderId"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_NestedTargetPath_CreatesNestedObject()
    {
        var input = ParseInput("""{"customer":{"full_name":"Alice"}}""");
        var config = Config(("$.customer.full_name", "customer.name"));

        var result = _engine.Transform(input, config);

        var customer = result["customer"]?.AsObject();
        Assert.NotNull(customer);
        Assert.Equal("Alice", customer["name"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_MissingSourceField_FieldOmittedFromOutput()
    {
        var input = ParseInput("""{"id":"123"}""");
        var config = Config(("$.id", "orderId"), ("$.nonexistent", "missing"));

        var result = _engine.Transform(input, config);

        Assert.Equal("123", result["orderId"]?.GetValue<string>());
        Assert.False(result.ContainsKey("missing"));
    }

    [Fact]
    public void Transform_DeeplyNestedTargetPath_CreatesFullNestedStructure()
    {
        var input = ParseInput("""{"address1":"123 Main St"}""");
        var config = Config(("$.address1", "shipping.address.line1"));

        var result = _engine.Transform(input, config);

        var shipping = result["shipping"]?.AsObject();
        var address = shipping?["address"]?.AsObject();
        Assert.Equal("123 Main St", address?["line1"]?.GetValue<string>());
    }
}
