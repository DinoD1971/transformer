using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Exceptions;
using Transformer.Functions;
using Transformer.Models;
using Transformer.Services;

namespace Transformer.Tests.Functions;

public class TransformFunctionTests
{
    private readonly Mock<IConfigLoader> _configLoaderMock;
    private readonly Mock<ITransformationEngine> _engineMock;
    private readonly TransformFunction _function;

    public TransformFunctionTests()
    {
        var logger = new Mock<ILogger<TransformFunction>>();
        _configLoaderMock = new Mock<IConfigLoader>();
        _engineMock = new Mock<ITransformationEngine>();

        _configLoaderMock
            .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransformConfig { Version = "1.0" }); // no mappings → passthrough

        _function = new TransformFunction(logger.Object, _configLoaderMock.Object, _engineMock.Object);
    }

    private static HttpRequest BuildRequest(string contentType, string? body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }
        return context.Request;
    }

    [Fact]
    public async Task RunAsync_ValidRequest_Returns200WithEnvelope()
    {
        var body = """{"correlationId":"test-123","payload":{"orderId":"abc"}}""";
        var req = BuildRequest("application/json", body);

        var result = await _function.RunAsync(req, "crm", "order", "sf-to-warehouse", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, content.StatusCode);
        Assert.Equal("application/json", content.ContentType);

        var doc = JsonDocument.Parse(content.Content!);
        var root = doc.RootElement;
        Assert.Equal("test-123", root.GetProperty("correlationId").GetString());
        Assert.Equal("crm", root.GetProperty("domain").GetString());
        Assert.Equal("order", root.GetProperty("operation").GetString());
        Assert.Equal("sf-to-warehouse", root.GetProperty("configName").GetString());
        Assert.True(root.TryGetProperty("processedAt", out _));
        Assert.Equal("abc", root.GetProperty("payload").GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task RunAsync_ConfigHasMappings_CallsEngine()
    {
        var configWithMappings = new TransformConfig
        {
            Version = "1.0",
            Mappings = [new MappingConfig { Source = "$.id", Target = "orderId" }]
        };
        _configLoaderMock
            .Setup(x => x.LoadAsync("crm", "order", "mapped", It.IsAny<CancellationToken>()))
            .ReturnsAsync(configWithMappings);

        var engineOutput = new JsonObject { ["orderId"] = "xyz" };
        _engineMock
            .Setup(x => x.Transform(It.IsAny<JsonObject>(), configWithMappings))
            .Returns(engineOutput);

        var body = """{"correlationId":"c1","payload":{"id":"xyz"}}""";
        var req = BuildRequest("application/json", body);

        var result = await _function.RunAsync(req, "crm", "order", "mapped", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, content.StatusCode);
        _engineMock.Verify(x => x.Transform(It.IsAny<JsonObject>(), configWithMappings), Times.Once);

        var doc = JsonDocument.Parse(content.Content!);
        Assert.Equal("xyz", doc.RootElement.GetProperty("payload").GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task RunAsync_MalformedBody_Returns400()
    {
        var req = BuildRequest("application/json", "not valid json {{");

        var result = await _function.RunAsync(req, "crm", "order", "sf-to-warehouse", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(400, content.StatusCode);

        var doc = JsonDocument.Parse(content.Content!);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://transformer/errors/invalid-request", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task RunAsync_WrongContentType_Returns415()
    {
        var req = BuildRequest("text/plain", """{"correlationId":"x","payload":{}}""");

        var result = await _function.RunAsync(req, "crm", "order", "sf-to-warehouse", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(415, content.StatusCode);

        var doc = JsonDocument.Parse(content.Content!);
        Assert.Equal(415, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://transformer/errors/unsupported-media-type", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task RunAsync_ConfigNotFound_Returns404()
    {
        _configLoaderMock
            .Setup(x => x.LoadAsync("crm", "order", "missing", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConfigNotFoundException("crm", "order", "missing"));

        var body = """{"correlationId":"abc","payload":{}}""";
        var req = BuildRequest("application/json", body);

        var result = await _function.RunAsync(req, "crm", "order", "missing", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(404, content.StatusCode);

        var doc = JsonDocument.Parse(content.Content!);
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://transformer/errors/config-not-found", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("abc", doc.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task RunAsync_ConfigParseError_Returns500()
    {
        _configLoaderMock
            .Setup(x => x.LoadAsync("crm", "order", "broken", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConfigParseException("crm", "order", "broken", "Unexpected token."));

        var body = """{"correlationId":"xyz","payload":{}}""";
        var req = BuildRequest("application/json", body);

        var result = await _function.RunAsync(req, "crm", "order", "broken", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(500, content.StatusCode);

        var doc = JsonDocument.Parse(content.Content!);
        Assert.Equal(500, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://transformer/errors/config-parse-error", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("xyz", doc.RootElement.GetProperty("correlationId").GetString());
    }
}
