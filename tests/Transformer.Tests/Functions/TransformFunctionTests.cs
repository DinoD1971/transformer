using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Functions;

namespace Transformer.Tests.Functions;

public class TransformFunctionTests
{
    private readonly TransformFunction _function;

    public TransformFunctionTests()
    {
        var logger = new Mock<ILogger<TransformFunction>>();
        _function = new TransformFunction(logger.Object);
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
}
