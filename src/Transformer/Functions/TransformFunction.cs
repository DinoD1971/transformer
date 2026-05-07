using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Transformer.Models;
using Transformer.Services;

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
    private readonly IConfigLoader _configLoader;
    private readonly ITransformationEngine _engine;

    public TransformFunction(ILogger<TransformFunction> logger, IConfigLoader configLoader, ITransformationEngine engine)
    {
        _logger = logger;
        _configLoader = configLoader;
        _engine = engine;
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

        var envelope = await JsonSerializer.DeserializeAsync<TransformRequest>(
            req.Body, SerializerOptions, cancellationToken);

        if (envelope is null)
            throw new ArgumentException("Request body must be a JSON object.");

        req.HttpContext.Items["CorrelationId"] = envelope.CorrelationId;

        _logger.LogInformation("Transform request received. CorrelationId={CorrelationId} Domain={Domain} Operation={Operation} ConfigName={ConfigName}",
            envelope.CorrelationId, domain, operation, configName);

        var config = await _configLoader.LoadAsync(domain, operation, configName, cancellationToken);

        JsonElement outputPayload;
        if (config.Mappings.Count > 0 && envelope.Payload.HasValue)
        {
            var inputNode = JsonNode.Parse(envelope.Payload.Value.GetRawText()) as JsonObject
                ?? new JsonObject();
            var outputNode = _engine.Transform(inputNode, config);
            outputPayload = JsonSerializer.Deserialize<JsonElement>(outputNode.ToJsonString());
        }
        else
        {
            outputPayload = envelope.Payload ?? default;
        }

        var response = new TransformResponse
        {
            CorrelationId = envelope.CorrelationId,
            Domain = domain,
            Operation = operation,
            ConfigName = configName,
            ProcessedAt = DateTime.UtcNow,
            Payload = outputPayload
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

    private static ContentResult ProblemResult(int status, string type, string title, string detail, string? correlationId = null)
    {
        var problem = new
        {
            type,
            title,
            status,
            detail,
            correlationId
        };
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(problem, SerializerOptions),
            ContentType = "application/json",
            StatusCode = status
        };
    }
}
