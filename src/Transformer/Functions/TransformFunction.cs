using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Transformer.Models;

namespace Transformer.Functions;

public class TransformFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<TransformFunction> _logger;

    public TransformFunction(ILogger<TransformFunction> logger)
    {
        _logger = logger;
    }

    [Function("Transform")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "transform/{domain}/{operation}/{configName}")]
        HttpRequest req,
        string domain,
        string operation,
        string configName,
        CancellationToken cancellationToken)
    {
        if (!IsJsonContentType(req.ContentType))
        {
            return ProblemResult(415,
                "https://transformer/errors/unsupported-media-type",
                "Unsupported Media Type",
                "Content-Type must be application/json.");
        }

        TransformRequest? envelope;
        try
        {
            envelope = await JsonSerializer.DeserializeAsync<TransformRequest>(
                req.Body, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize request body");
            return ProblemResult(400,
                "https://transformer/errors/invalid-request",
                "Invalid Request",
                "Request body is not valid JSON.");
        }

        if (envelope is null)
        {
            return ProblemResult(400,
                "https://transformer/errors/invalid-request",
                "Invalid Request",
                "Request body must be a JSON object.");
        }

        _logger.LogInformation("Transform request received. CorrelationId={CorrelationId} Domain={Domain} Operation={Operation} ConfigName={ConfigName}",
            envelope.CorrelationId, domain, operation, configName);

        var response = new TransformResponse
        {
            CorrelationId = envelope.CorrelationId,
            Domain = domain,
            Operation = operation,
            ConfigName = configName,
            ProcessedAt = DateTime.UtcNow,
            Payload = envelope.Payload
        };

        _logger.LogInformation("Transform request completed. CorrelationId={CorrelationId}", envelope.CorrelationId);

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    private static bool IsJsonContentType(string? contentType) =>
        contentType != null &&
        (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
         contentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase));

    private static ContentResult ProblemResult(int status, string type, string title, string detail)
    {
        var problem = new
        {
            type,
            title,
            status,
            detail
        };
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(problem, SerializerOptions),
            ContentType = "application/json",
            StatusCode = status
        };
    }
}
