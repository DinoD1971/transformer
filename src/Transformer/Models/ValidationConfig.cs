namespace Transformer.Models;

public class ValidationConfig
{
    public string? Regex { get; set; }
    public string OnFail { get; set; } = "null";
}
