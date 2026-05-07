using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
using Microsoft.Extensions.Logging;
using Transformer.Exceptions;
using Transformer.Models;

namespace Transformer.Services;

public class TransformationEngine : ITransformationEngine
{
    private readonly ILogger<TransformationEngine> _logger;
    private readonly TransformRegistry _registry;

    public TransformationEngine(ILogger<TransformationEngine> logger, TransformRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public JsonObject Transform(JsonObject input, TransformConfig config)
    {
        var output = new JsonObject();
        var mismatchMode = config.ErrorHandling?.OnTypeMismatch ?? "coerce";
        var dateFormat = config.Settings?.DateFormat;

        foreach (var mapping in config.Mappings)
        {
            if (string.IsNullOrEmpty(mapping.Target))
                continue;

            var hasDefault = mapping.Default.HasValue && mapping.Default.Value.ValueKind != JsonValueKind.Undefined;

            bool sourceFound = false;
            JsonNode? value = null;

            if (!string.IsNullOrEmpty(mapping.Source))
                (sourceFound, value) = ResolveSource(mapping.Source, input);

            JsonNode? resolvedValue;
            if (sourceFound && value is not null)
            {
                resolvedValue = value;
            }
            else if (hasDefault)
            {
                resolvedValue = JsonNode.Parse(mapping.Default!.Value.GetRawText());
            }
            else if (sourceFound)
            {
                resolvedValue = null;
            }
            else
            {
                _logger.LogWarning("Source path '{Source}' matched no value and no default — field omitted.", mapping.Source);
                continue;
            }

            var afterType = resolvedValue is null ? null : ApplyType(resolvedValue.DeepClone(), mapping, mismatchMode, dateFormat);
            var afterTransform = ApplyTransform(afterType, mapping, dateFormat);
            var finalValue = ApplyValidation(afterTransform, mapping);

            SetNestedValue(output, mapping.Target, finalValue);
        }

        if (config.Settings?.IgnoreNulls == true)
            RemoveNullFields(output);

        return output;
    }

    private JsonNode? ApplyType(JsonNode value, MappingConfig mapping, string mismatchMode, string? dateFormat)
    {
        if (string.IsNullOrEmpty(mapping.Type))
            return value;

        var result = TypeConverter.Convert(value, mapping.Type, dateFormat);
        if (result.Succeeded)
            return result.Value;

        return mismatchMode.ToLowerInvariant() switch
        {
            "error" => throw new TransformationException(
                $"Type mismatch on target '{mapping.Target}': {result.ErrorMessage}"),
            "null" => null,
            _ => LogCoercionAndReturnNull(mapping.Target, result.ErrorMessage)
        };
    }

    private JsonNode? ApplyTransform(JsonNode? value, MappingConfig mapping, string? dateFormat)
    {
        if (string.IsNullOrEmpty(mapping.Transform))
            return value;

        var fn = _registry.Resolve(mapping.Transform);
        return fn.Execute(value, mapping.Parameters, dateFormat);
    }

    private JsonNode? ApplyValidation(JsonNode? value, MappingConfig mapping)
    {
        if (mapping.Validate is null || string.IsNullOrEmpty(mapping.Validate.Regex) || value is null)
            return value;

        var str = value is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : value.ToJsonString();
        var regex = RegexCache.Get(mapping.Validate.Regex);

        if (regex.IsMatch(str))
            return value;

        return (mapping.Validate.OnFail ?? "null").ToLowerInvariant() switch
        {
            "error" => throw new TransformationException(
                $"Validation failed for target '{mapping.Target}': value '{str}' did not match regex '{mapping.Validate.Regex}'."),
            "default" => ResolveValidationDefault(mapping),
            _ => LogValidationFailureAndReturnNull(mapping.Target, str)
        };
    }

    private JsonNode? ResolveValidationDefault(MappingConfig mapping)
    {
        var hasDefault = mapping.Default.HasValue && mapping.Default.Value.ValueKind != System.Text.Json.JsonValueKind.Undefined;
        if (!hasDefault)
        {
            _logger.LogWarning("Validation failed for target '{Target}' and onFail is 'default' but no default is set — writing null.", mapping.Target);
            return null;
        }

        _logger.LogWarning("Validation failed for target '{Target}' — writing default value.", mapping.Target);
        return JsonNode.Parse(mapping.Default!.Value.GetRawText());
    }

    private JsonNode? LogValidationFailureAndReturnNull(string target, string value)
    {
        _logger.LogWarning("Validation failed for target '{Target}': value '{Value}' did not match regex — writing null.", target, value);
        return null;
    }

    private JsonNode? LogCoercionAndReturnNull(string target, string? errorMessage)
    {
        _logger.LogWarning("Type coercion failed for target '{Target}': {Error} — writing null.", target, errorMessage);
        return null;
    }

    private (bool found, JsonNode? value) ResolveSource(string sourcePath, JsonNode input)
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
        var match = result.Matches.FirstOrDefault();
        return match is null ? (false, null) : (true, match.Value);
    }

    private static void SetNestedValue(JsonObject obj, string dotPath, JsonNode? value)
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

    private static void RemoveNullFields(JsonObject obj)
    {
        var nullKeys = obj.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
        foreach (var key in nullKeys)
            obj.Remove(key);

        foreach (var kv in obj)
        {
            if (kv.Value is JsonObject nested)
                RemoveNullFields(nested);
        }
    }
}
