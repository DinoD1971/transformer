using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Exceptions;
using Transformer.Models;
using Transformer.Services;
using Transformer.Services.TransformFunctions;

namespace Transformer.Tests.Services;

public class TransformationEngineTests
{
    private readonly TransformationEngine _engine;

    private static TransformRegistry BuildRegistry() => new(
    [
        ("trim",     new TrimTransformFunction()),
        ("round",    new RoundTransformFunction()),
        ("contains", new ContainsTransformFunction()),
        ("now",      new NowTransformFunction())
    ]);

    public TransformationEngineTests()
    {
        var logger = new Mock<ILogger<TransformationEngine>>();
        _engine = new TransformationEngine(logger.Object, BuildRegistry());
    }

    private static JsonObject ParseInput(string json) =>
        JsonNode.Parse(json)!.AsObject();

    private static TransformConfig Config(params (string source, string target)[] mappings) =>
        new()
        {
            Mappings = mappings.Select(m => new MappingConfig { Source = m.source, Target = m.target }).ToList()
        };

    private static TransformConfig TypedConfig(string source, string target, string type, string? mismatchMode = null) =>
        new()
        {
            Mappings = [new MappingConfig { Source = source, Target = target, Type = type }],
            ErrorHandling = mismatchMode is null ? null : new ErrorHandlingConfig { OnTypeMismatch = mismatchMode }
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

    // --- type conversion + onTypeMismatch modes ---

    [Fact]
    public void Transform_TypeConversion_HappyPath_WritesConvertedValue()
    {
        var input = ParseInput("""{"amount":"49.99"}""");
        var config = TypedConfig("$.amount", "total", "decimal");

        var result = _engine.Transform(input, config);

        Assert.Equal(49.99m, result["total"]?.GetValue<decimal>());
    }

    [Fact]
    public void Transform_OnTypeMismatch_Error_ThrowsTransformationException()
    {
        var input = ParseInput("""{"amount":"not-a-number"}""");
        var config = TypedConfig("$.amount", "total", "decimal", "error");

        Assert.Throws<TransformationException>(() => _engine.Transform(input, config));
    }

    [Fact]
    public void Transform_OnTypeMismatch_Null_WritesNullToTarget()
    {
        var input = ParseInput("""{"amount":"not-a-number"}""");
        var config = TypedConfig("$.amount", "total", "decimal", "null");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("total"));
        Assert.Null(result["total"]);
    }

    [Fact]
    public void Transform_OnTypeMismatch_Coerce_WritesNullToTarget()
    {
        var input = ParseInput("""{"amount":"not-a-number"}""");
        var config = TypedConfig("$.amount", "total", "decimal", "coerce");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("total"));
        Assert.Null(result["total"]);
    }

    // --- default values ---

    [Fact]
    public void Transform_Default_SourceMissing_UsesDefault()
    {
        var input = ParseInput("""{"id":"1"}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig { Source = "$.currency", Target = "currency", Default = JsonDocument.Parse("\"USD\"").RootElement }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal("USD", result["currency"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Default_SourceNull_UsesDefault()
    {
        var input = ParseInput("""{"currency":null}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig { Source = "$.currency", Target = "currency", Default = JsonDocument.Parse("\"USD\"").RootElement }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal("USD", result["currency"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Default_WithTypeConversion_ConvertsDefault()
    {
        var input = ParseInput("""{"id":"1"}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig { Source = "$.price", Target = "price", Type = "decimal", Default = JsonDocument.Parse("\"99\"").RootElement }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal(99m, result["price"]?.GetValue<decimal>());
    }

    // --- ignoreNulls ---

    [Fact]
    public void Transform_IgnoreNulls_True_OmitsNullFields()
    {
        var input = ParseInput("""{"id":"1","note":null}""");
        var config = new TransformConfig
        {
            Settings = new TransformSettings { IgnoreNulls = true },
            Mappings =
            [
                new MappingConfig { Source = "$.id", Target = "id" },
                new MappingConfig { Source = "$.note", Target = "note" }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("id"));
        Assert.False(result.ContainsKey("note"));
    }

    [Fact]
    public void Transform_IgnoreNulls_False_WritesNullFields()
    {
        var input = ParseInput("""{"id":"1","note":null}""");
        var config = new TransformConfig
        {
            Settings = new TransformSettings { IgnoreNulls = false },
            Mappings =
            [
                new MappingConfig { Source = "$.id", Target = "id" },
                new MappingConfig { Source = "$.note", Target = "note" }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("id"));
        Assert.True(result.ContainsKey("note"));
        Assert.Null(result["note"]);
    }

    // --- transform functions via engine ---

    [Fact]
    public void Transform_TrimFunction_TrimsValue()
    {
        var input = ParseInput("""{"name":"  Alice  "}""");
        var config = new TransformConfig
        {
            Mappings = [new MappingConfig { Source = "$.name", Target = "name", Transform = "trim" }]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal("Alice", result["name"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_UnknownTransformName_ThrowsTransformationException()
    {
        var input = ParseInput("""{"x":"1"}""");
        var config = new TransformConfig
        {
            Mappings = [new MappingConfig { Source = "$.x", Target = "x", Transform = "nonexistent" }]
        };

        Assert.Throws<TransformationException>(() => _engine.Transform(input, config));
    }

    // --- validation ---

    private static TransformConfig ValidationConfig(string source, string target, string regex, string onFail, string? defaultJson = null) =>
        new()
        {
            Mappings =
            [
                new MappingConfig
                {
                    Source = source,
                    Target = target,
                    Validate = new Transformer.Models.ValidationConfig { Regex = regex, OnFail = onFail },
                    Default = defaultJson is null ? null : JsonDocument.Parse(defaultJson).RootElement
                }
            ]
        };

    [Fact]
    public void Transform_Validation_RegexMatch_WritesValue()
    {
        var input = ParseInput("""{"email":"user@example.com"}""");
        var config = ValidationConfig("$.email", "email", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "null");

        var result = _engine.Transform(input, config);

        Assert.Equal("user@example.com", result["email"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Validation_OnFail_Null_WritesNull()
    {
        var input = ParseInput("""{"email":"not-an-email"}""");
        var config = ValidationConfig("$.email", "email", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "null");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("email"));
        Assert.Null(result["email"]);
    }

    [Fact]
    public void Transform_Validation_OnFail_Error_ThrowsTransformationException()
    {
        var input = ParseInput("""{"email":"not-an-email"}""");
        var config = ValidationConfig("$.email", "email", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "error");

        Assert.Throws<TransformationException>(() => _engine.Transform(input, config));
    }

    [Fact]
    public void Transform_Validation_OnFail_Default_WithDefault_WritesDefault()
    {
        var input = ParseInput("""{"email":"not-an-email"}""");
        var config = ValidationConfig("$.email", "email", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "default", "\"unknown@example.com\"");

        var result = _engine.Transform(input, config);

        Assert.Equal("unknown@example.com", result["email"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Validation_OnFail_Default_NoDefault_WritesNull()
    {
        var input = ParseInput("""{"email":"not-an-email"}""");
        var config = ValidationConfig("$.email", "email", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "default");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("email"));
        Assert.Null(result["email"]);
    }

    [Fact]
    public void Transform_Validation_NullValue_SkipsValidation()
    {
        var input = ParseInput("""{"email":null}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig
                {
                    Source = "$.email",
                    Target = "email",
                    Validate = new Transformer.Models.ValidationConfig { Regex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", OnFail = "error" }
                }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("email"));
        Assert.Null(result["email"]);
    }
}
