using System.Text.Json.Nodes;
using Json.Path;
using Microsoft.Extensions.Logging;
using Transformer.Exceptions;
using Transformer.Models;

namespace Transformer.Services;

public class TransformationEngine : ITransformationEngine
{
    private readonly ILogger<TransformationEngine> _logger;

    public TransformationEngine(ILogger<TransformationEngine> logger)
    {
        _logger = logger;
    }

    public JsonObject Transform(JsonObject input, TransformConfig config)
    {
        var output = new JsonObject();

        foreach (var mapping in config.Mappings)
        {
            if (string.IsNullOrEmpty(mapping.Source) || string.IsNullOrEmpty(mapping.Target))
                continue;

            JsonNode? value = ResolveSource(mapping.Source, input);
            if (value is null)
            {
                _logger.LogWarning("Source path '{Source}' matched no value — field omitted.", mapping.Source);
                continue;
            }

            SetNestedValue(output, mapping.Target, value.DeepClone());
        }

        return output;
    }

    private JsonNode? ResolveSource(string sourcePath, JsonNode input)
    {
        JsonPath path;
        try
        {
            path = JsonPath.Parse(sourcePath);
        }
        catch (Exception ex)
        {
            throw new TransformationException($"Invalid JSONPath '{sourcePath}': {ex.Message}", ex);
        }

        var result = path.Evaluate(input);
        return result.Matches.FirstOrDefault()?.Value;
    }

    private static void SetNestedValue(JsonObject obj, string dotPath, JsonNode value)
    {
        var parts = dotPath.Split('.');
        var current = obj;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!current.TryGetPropertyValue(parts[i], out var next) || next is not JsonObject child)
            {
                child = new JsonObject();
                current[parts[i]] = child;
            }
            current = child;
        }

        current[parts[^1]] = value;
    }
}
