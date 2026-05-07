using System.Text.Json;

namespace Transformer.Models;

public class ConditionConfig
{
    public string? If { get; set; }
    public JsonElement? Then { get; set; }
    public JsonElement? Else { get; set; }
}
