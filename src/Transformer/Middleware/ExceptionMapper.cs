using System.Text.Json;
using Transformer.Exceptions;

namespace Transformer.Middleware;

public static class ExceptionMapper
{
    public record MappedProblem(int Status, string Type, string Title);

    public static MappedProblem Map(Exception ex) => ex switch
    {
        ConfigNotFoundException   => new(404, "https://transformer/errors/config-not-found",    "Configuration Not Found"),
        ConfigParseException      => new(500, "https://transformer/errors/config-parse-error",  "Configuration Parse Error"),
        TransformationException   => new(422, "https://transformer/errors/transformation-error","Transformation Error"),
        ArgumentException         => new(400, "https://transformer/errors/invalid-request",     "Invalid Request"),
        JsonException             => new(400, "https://transformer/errors/invalid-request",     "Invalid Request"),
        _                         => new(500, "https://transformer/errors/internal-error",      "Internal Server Error")
    };
}
