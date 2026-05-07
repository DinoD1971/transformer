using System.Text.Json;

namespace Transformer.Models;

public class TransformResponse
{
    public string? CorrelationId { get; set; }
    public string? Domain { get; set; }
    public string? Operation { get; set; }
    public string? ConfigName { get; set; }
    public DateTime ProcessedAt { get; set; }
    public JsonElement? Payload { get; set; }
}
