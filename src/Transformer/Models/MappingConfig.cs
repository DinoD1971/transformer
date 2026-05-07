using System.Text.Json;

namespace Transformer.Models;

public class MappingConfig
{
    public string? Source { get; set; }
    public string Target { get; set; } = string.Empty;
    public string? Type { get; set; }
    public JsonElement? Default { get; set; }
    public string? Transform { get; set; }
    public JsonElement? Parameters { get; set; }
    public ValidationConfig? Validate { get; set; }
}
