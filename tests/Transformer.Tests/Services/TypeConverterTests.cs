using System.Text.Json.Nodes;
using Transformer.Services;

namespace Transformer.Tests.Services;

public class TypeConverterTests
{
    private static JsonNode Val(string s) => JsonValue.Create(s)!;
    private static JsonNode Val(double d) => JsonValue.Create(d)!;
    private static JsonNode Val(bool b) => JsonValue.Create(b)!;

    // --- string ---

    [Fact]
    public void Convert_StringType_StringInput_ReturnsString()
    {
        var result = TypeConverter.Convert(Val("hello"), "string", null);
        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.Value!.GetValue<string>());
    }

    [Fact]
    public void Convert_StringType_NumberInput_ReturnsJsonString()
    {
        var result = TypeConverter.Convert(Val(42.0), "string", null);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
    }

    // --- decimal ---

    [Fact]
    public void Convert_DecimalType_NumericInput_ReturnsDecimal()
    {
        var result = TypeConverter.Convert(Val(123.45), "decimal", null);
        Assert.True(result.Succeeded);
        Assert.Equal(123.45m, result.Value!.GetValue<decimal>());
    }

    [Fact]
    public void Convert_DecimalType_StringNumber_CoercesToDecimal()
    {
        var result = TypeConverter.Convert(Val("99.9"), "decimal", null);
        Assert.True(result.Succeeded);
        Assert.Equal(99.9m, result.Value!.GetValue<decimal>());
    }

    [Fact]
    public void Convert_DecimalType_EmptyString_Fails()
    {
        var result = TypeConverter.Convert(Val(""), "decimal", null);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    // --- integer ---

    [Fact]
    public void Convert_IntegerType_NumericInput_ReturnsLong()
    {
        var result = TypeConverter.Convert(Val(7.0), "integer", null);
        Assert.True(result.Succeeded);
        Assert.Equal(7L, result.Value!.GetValue<long>());
    }

    [Fact]
    public void Convert_IntegerType_StringNumber_CoercesToLong()
    {
        var result = TypeConverter.Convert(Val("42"), "integer", null);
        Assert.True(result.Succeeded);
        Assert.Equal(42L, result.Value!.GetValue<long>());
    }

    // --- boolean ---

    [Fact]
    public void Convert_BooleanType_BoolInput_ReturnsBoolean()
    {
        var result = TypeConverter.Convert(Val(true), "boolean", null);
        Assert.True(result.Succeeded);
        Assert.True(result.Value!.GetValue<bool>());
    }

    [Fact]
    public void Convert_BooleanType_StringTrue_ReturnsTrue()
    {
        var result = TypeConverter.Convert(Val("true"), "boolean", null);
        Assert.True(result.Succeeded);
        Assert.True(result.Value!.GetValue<bool>());
    }

    [Fact]
    public void Convert_BooleanType_StringFalse_ReturnsFalse()
    {
        var result = TypeConverter.Convert(Val("false"), "boolean", null);
        Assert.True(result.Succeeded);
        Assert.False(result.Value!.GetValue<bool>());
    }

    // --- datetime ---

    [Fact]
    public void Convert_DatetimeType_Iso8601String_ReturnsFormattedString()
    {
        var result = TypeConverter.Convert(Val("2024-03-15T10:00:00Z"), "datetime", null);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value?.GetValue<string>());
    }

    [Fact]
    public void Convert_DatetimeType_WithDateFormat_UsesFormat()
    {
        var result = TypeConverter.Convert(Val("15/03/2024"), "datetime", "dd/MM/yyyy");
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value?.GetValue<string>());
    }

    [Fact]
    public void Convert_DatetimeType_InvalidString_Fails()
    {
        var result = TypeConverter.Convert(Val("not-a-date"), "datetime", null);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }
}
