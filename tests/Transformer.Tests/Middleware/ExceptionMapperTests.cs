using System.Text.Json;
using Transformer.Exceptions;
using Transformer.Middleware;

namespace Transformer.Tests.Middleware;

public class ExceptionMapperTests
{
    [Fact]
    public void Map_ConfigNotFoundException_Returns404WithCorrectTypeAndTitle()
    {
        var result = ExceptionMapper.Map(new ConfigNotFoundException("d", "op", "cfg"));

        Assert.Equal(404, result.Status);
        Assert.Equal("https://transformer/errors/config-not-found", result.Type);
        Assert.Equal("Configuration Not Found", result.Title);
    }

    [Fact]
    public void Map_ConfigParseException_Returns500WithCorrectTypeAndTitle()
    {
        var result = ExceptionMapper.Map(new ConfigParseException("d", "op", "cfg", "bad json"));

        Assert.Equal(500, result.Status);
        Assert.Equal("https://transformer/errors/config-parse-error", result.Type);
        Assert.Equal("Configuration Parse Error", result.Title);
    }

    [Fact]
    public void Map_TransformationException_Returns422WithCorrectTypeAndTitle()
    {
        var result = ExceptionMapper.Map(new TransformationException("type mismatch"));

        Assert.Equal(422, result.Status);
        Assert.Equal("https://transformer/errors/transformation-error", result.Type);
        Assert.Equal("Transformation Error", result.Title);
    }

    [Fact]
    public void Map_ArgumentException_Returns400WithCorrectTypeAndTitle()
    {
        var result = ExceptionMapper.Map(new ArgumentException("bad arg"));

        Assert.Equal(400, result.Status);
        Assert.Equal("https://transformer/errors/invalid-request", result.Type);
        Assert.Equal("Invalid Request", result.Title);
    }

    [Fact]
    public void Map_JsonException_Returns400WithCorrectTypeAndTitle()
    {
        var result = ExceptionMapper.Map(new JsonException("invalid json"));

        Assert.Equal(400, result.Status);
        Assert.Equal("https://transformer/errors/invalid-request", result.Type);
        Assert.Equal("Invalid Request", result.Title);
    }

    [Fact]
    public void Map_UnhandledException_Returns500WithInternalErrorType()
    {
        var result = ExceptionMapper.Map(new InvalidOperationException("unexpected"));

        Assert.Equal(500, result.Status);
        Assert.Equal("https://transformer/errors/internal-error", result.Type);
        Assert.Equal("Internal Server Error", result.Title);
    }

    [Theory]
    [InlineData(typeof(ConfigNotFoundException))]
    [InlineData(typeof(TransformationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(JsonException))]
    public void Map_ClientErrors_HaveStatusBelow500(Type exceptionType)
    {
        var ex = exceptionType == typeof(ConfigNotFoundException)
            ? (Exception)new ConfigNotFoundException("d", "op", "cfg")
            : exceptionType == typeof(TransformationException)
            ? new TransformationException("msg")
            : exceptionType == typeof(ArgumentException)
            ? new ArgumentException("msg")
            : new JsonException("msg");

        Assert.True(ExceptionMapper.Map(ex).Status < 500);
    }

    [Theory]
    [InlineData(typeof(ConfigParseException))]
    [InlineData(typeof(InvalidOperationException))]
    public void Map_ServerErrors_HaveStatus500(Type exceptionType)
    {
        var ex = exceptionType == typeof(ConfigParseException)
            ? (Exception)new ConfigParseException("d", "op", "cfg", "detail")
            : new InvalidOperationException("msg");

        Assert.Equal(500, ExceptionMapper.Map(ex).Status);
    }
}
