namespace Transformer.Models;

public class ErrorHandlingConfig
{
    public string OnTypeMismatch { get; set; } = "coerce";
    public string? OnMissingField { get; set; }
    public string? OnError { get; set; }
}
