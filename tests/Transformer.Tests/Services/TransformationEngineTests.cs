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
        var conditionLogger = new Mock<ILogger<ConditionEvaluator>>();
        _engine = new TransformationEngine(
            logger.Object,
            BuildRegistry(),
            new ConditionEvaluator(conditionLogger.Object),
            new ExpressionEvaluator());
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

    // --- lookup ---

    private static TransformConfig LookupConfig(
        string source, string target,
        Dictionary<string, string> lookup,
        string? onMissingField = null) =>
        new()
        {
            Mappings = [new MappingConfig { Source = source, Target = target, Lookup = lookup }],
            ErrorHandling = onMissingField is null ? null : new ErrorHandlingConfig { OnMissingField = onMissingField }
        };

    [Fact]
    public void Transform_Lookup_Hit_WritesMappedValue()
    {
        var input = ParseInput("""{"status":"paid"}""");
        var config = LookupConfig("$.status", "status",
            new() { { "pending", "Pending" }, { "paid", "Completed" }, { "failed", "Cancelled" } });

        var result = _engine.Transform(input, config);

        Assert.Equal("Completed", result["status"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Lookup_Miss_OnMissingField_Ignore_WritesOriginalValue()
    {
        var input = ParseInput("""{"status":"unknown"}""");
        var config = LookupConfig("$.status", "status",
            new() { { "paid", "Completed" } }, "ignore");

        var result = _engine.Transform(input, config);

        Assert.Equal("unknown", result["status"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Lookup_Miss_OnMissingField_Error_ThrowsTransformationException()
    {
        var input = ParseInput("""{"status":"unknown"}""");
        var config = LookupConfig("$.status", "status",
            new() { { "paid", "Completed" } }, "error");

        Assert.Throws<TransformationException>(() => _engine.Transform(input, config));
    }

    [Fact]
    public void Transform_Lookup_Miss_OnMissingField_Null_WritesNull()
    {
        var input = ParseInput("""{"status":"unknown"}""");
        var config = LookupConfig("$.status", "status",
            new() { { "paid", "Completed" } }, "null");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("status"));
        Assert.Null(result["status"]);
    }

    // --- conditional logic ---

    private static TransformConfig ConditionMappingConfig(string ifExpr, string? thenJson, string? elseJson, string? type = null) =>
        new()
        {
            Mappings =
            [
                new MappingConfig
                {
                    Target = "result",
                    Type = type,
                    Condition = new Transformer.Models.ConditionConfig
                    {
                        If = ifExpr,
                        Then = thenJson is null ? null : JsonDocument.Parse(thenJson).RootElement,
                        Else = elseJson is null ? null : JsonDocument.Parse(elseJson).RootElement
                    }
                }
            ]
        };

    [Fact]
    public void Transform_Condition_TrueBranch_WritesLiteralThenValue()
    {
        var input = ParseInput("""{"total":1500}""");
        var config = ConditionMappingConfig("$.total > 1000", "\"high\"", "\"low\"");

        var result = _engine.Transform(input, config);

        Assert.Equal("high", result["result"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Condition_FalseBranch_WritesLiteralElseValue()
    {
        var input = ParseInput("""{"total":500}""");
        var config = ConditionMappingConfig("$.total > 1000", "\"high\"", "\"low\"");

        var result = _engine.Transform(input, config);

        Assert.Equal("low", result["result"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Condition_ThenIsJsonPath_ResolvesValue()
    {
        var input = ParseInput("""{"discount":{"amount":10.0},"flag":true}""");
        var config = ConditionMappingConfig("$.flag == true", "\"$.discount.amount\"", "\"0\"");

        var result = _engine.Transform(input, config);

        Assert.NotNull(result["result"]);
        Assert.Equal(10.0, result["result"]!.GetValue<double>(), precision: 5);
    }

    [Fact]
    public void Transform_Condition_ElseIsJsonPath_ResolvesValue()
    {
        var input = ParseInput("""{"discount":{"amount":10.0},"flag":false}""");
        var config = ConditionMappingConfig("$.flag == true", "\"$.discount.amount\"", "\"0\"");

        var result = _engine.Transform(input, config);

        Assert.Equal("0", result["result"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Condition_WithTypeConversion_ConvertsSelectedBranch()
    {
        var input = ParseInput("""{"total":1500}""");
        var config = ConditionMappingConfig("$.total > 1000", "\"99.5\"", "\"0\"", type: "decimal");

        var result = _engine.Transform(input, config);

        Assert.Equal(99.5m, result["result"]?.GetValue<decimal>());
    }

    [Fact]
    public void Transform_Condition_SourceFieldIgnored()
    {
        var input = ParseInput("""{"total":1500,"ignored":"should-not-appear"}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig
                {
                    Source = "$.ignored",
                    Target = "result",
                    Condition = new Transformer.Models.ConditionConfig
                    {
                        If = "$.total > 1000",
                        Then = JsonDocument.Parse("\"high\"").RootElement
                    }
                }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal("high", result["result"]?.GetValue<string>());
    }

    [Fact]
    public void Transform_Condition_NestedObjectSource_Evaluates()
    {
        var input = ParseInput("""{"order":{"total":2000}}""");
        var config = ConditionMappingConfig("$.order.total >= 1000", "\"premium\"", "\"standard\"");

        var result = _engine.Transform(input, config);

        Assert.Equal("premium", result["result"]?.GetValue<string>());
    }

    // --- inline expressions ---

    private static TransformConfig ExpressionMappingConfig(string expression, string target, string? type = null,
        string? onMissingField = null) =>
        new()
        {
            Mappings = [new MappingConfig { Target = target, Expression = expression, Type = type }],
            ErrorHandling = onMissingField is null ? null : new ErrorHandlingConfig { OnMissingField = onMissingField }
        };

    [Fact]
    public void Transform_Expression_Arithmetic_WritesResult()
    {
        var input = ParseInput("""{"qty":3,"price":9.99}""");
        var config = ExpressionMappingConfig("$.qty * $.price", "lineTotal", "decimal");

        var result = _engine.Transform(input, config);

        Assert.Equal(29.97m, result["lineTotal"]?.GetValue<decimal>());
    }

    [Fact]
    public void Transform_Expression_Comparison_WritesBool()
    {
        var input = ParseInput("""{"total":1500}""");
        var config = ExpressionMappingConfig("$.total > 1000", "isHighValue", "boolean");

        var result = _engine.Transform(input, config);

        Assert.True(result["isHighValue"]?.GetValue<bool>());
    }

    [Fact]
    public void Transform_Expression_WithTypeConversion_ConvertsResult()
    {
        var input = ParseInput("""{"a":"5","b":"3"}""");
        var config = ExpressionMappingConfig("$.a + $.b", "total", "decimal");

        var result = _engine.Transform(input, config);

        Assert.Equal(8m, result["total"]?.GetValue<decimal>());
    }

    [Fact]
    public void Transform_Expression_MissingOperand_OnMissingFieldError_Throws()
    {
        var input = ParseInput("""{"qty":3}""");
        var config = ExpressionMappingConfig("$.qty * $.price", "lineTotal", null, "error");

        Assert.Throws<TransformationException>(() => _engine.Transform(input, config));
    }

    [Fact]
    public void Transform_Expression_MissingOperand_OnMissingFieldNull_WritesNull()
    {
        var input = ParseInput("""{"qty":3}""");
        var config = ExpressionMappingConfig("$.qty * $.price", "lineTotal", null, "null");

        var result = _engine.Transform(input, config);

        Assert.True(result.ContainsKey("lineTotal"));
        Assert.Null(result["lineTotal"]);
    }

    [Fact]
    public void Transform_Expression_MissingOperand_OnMissingFieldIgnore_OmitsField()
    {
        var input = ParseInput("""{"qty":3}""");
        var config = ExpressionMappingConfig("$.qty * $.price", "lineTotal", null, "ignore");

        var result = _engine.Transform(input, config);

        Assert.False(result.ContainsKey("lineTotal"));
    }

    [Fact]
    public void Transform_Expression_SourceFieldIgnored()
    {
        var input = ParseInput("""{"qty":2,"price":5,"ignored":"nope"}""");
        var config = new TransformConfig
        {
            Mappings =
            [
                new MappingConfig { Source = "$.ignored", Target = "total", Expression = "$.qty * $.price" }
            ]
        };

        var result = _engine.Transform(input, config);

        Assert.Equal(10m, result["total"]?.GetValue<decimal>());
    }
}
