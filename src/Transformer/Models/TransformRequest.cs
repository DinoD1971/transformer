using System.Text.Json;

namespace Transformer.Models;

public class TransformRequest
{
    public string? CorrelationId { get; set; }
    public JsonElement? Payload { get; set; }
}
