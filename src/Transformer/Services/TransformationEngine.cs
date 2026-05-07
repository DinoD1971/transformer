using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.Path;
using Microsoft.Extensions.Logging;
using Transformer.Exceptions;
using Transformer.Models;

namespace Transformer.Services;

public class TransformationEngine : ITransformationEngine
{
    private static readonly JsonSerializerOptions ItemMappingDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<TransformationEngine> _logger;
    private readonly TransformRegistry _registry;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly IExpressionEvaluator _expressionEvaluator;

    public TransformationEngine(ILogger<TransformationEngine> logger, TransformRegistry registry,
        IConditionEvaluator conditionEvaluator, IExpressionEvaluator expressionEvaluator)
    {
        _logger = logger;
        _registry = registry;
        _conditionEvaluator = conditionEvaluator;
        _expressionEvaluator = expressionEvaluator;
    }

    public JsonObject Transform(JsonObject input, TransformConfig config)
    {
        var output = new JsonObject();
        var mismatchMode = config.ErrorHandling?.OnTypeMismatch ?? "coerce";
        var missingFieldMode = config.ErrorHandling?.OnMissingField ?? "ignore";
        var dateFormat = config.Settings?.DateFormat;

        foreach (var mapping in config.Mappings)
        {
            if (string.IsNullOrEmpty(mapping.Target))
                continue;

            var hasDefault = mapping.Default.HasValue && mapping.Default.Value.ValueKind != JsonValueKind.Undefined;

            if (mapping.Type?.Equals("array", StringComparison.OrdinalIgnoreCase) == true
                && mapping.ItemMapping is not null)
            {
                var arrayResult = ApplyArrayMapping(mapping, input, config, mismatchMode);
                if (arrayResult is not null)
                    SetNestedValue(output, mapping.Target, arrayResult);
                continue;
            }

            JsonNode? resolvedValue;

            if (mapping.Condition is not null)
            {
                resolvedValue = ResolveCondition(mapping.Condition, input);
            }
            else if (!string.IsNullOrEmpty(mapping.Expression))
            {
                try
                {
                    resolvedValue = _expressionEvaluator.Evaluate(mapping.Expression, input);
                }
                catch (TransformationException) when (missingFieldMode.Equals("ignore", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Expression '{Expression}' skipped — operand resolved to null or absent.", mapping.Expression);
                    continue;
                }
                catch (TransformationException) when (missingFieldMode.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Expression '{Expression}' — operand resolved to null or absent, writing null.", mapping.Expression);
                    resolvedValue = null;
                }
            }
            else
            {
                // value takes precedence over source when both are present
                var hasStaticValue = mapping.Value.HasValue && mapping.Value.Value.ValueKind != JsonValueKind.Undefined;

                if (hasStaticValue)
                {
                    resolvedValue = mapping.Value!.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : JsonNode.Parse(mapping.Value!.Value.GetRawText());
                }
                else
                {
                    bool sourceFound = false;
                    JsonNode? value = null;

                    if (!string.IsNullOrEmpty(mapping.Source))
                        (sourceFound, value) = ResolveSource(mapping.Source, input);

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
                }
            }

            var afterType = resolvedValue is null ? null : ApplyType(resolvedValue.DeepClone(), mapping, mismatchMode, dateFormat);
            var afterLookup = ApplyLookup(afterType, mapping, missingFieldMode);
            var afterTransform = ApplyTransform(afterLookup, mapping, dateFormat);
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

    private JsonNode? ApplyLookup(JsonNode? value, MappingConfig mapping, string missingFieldMode)
    {
        if (mapping.Lookup is null || mapping.Lookup.Count == 0 || value is null)
            return value;

        var key = value is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : value.ToJsonString();

        if (mapping.Lookup.TryGetValue(key, out var mapped))
            return JsonValue.Create(mapped);

        return missingFieldMode.ToLowerInvariant() switch
        {
            "error" => throw new TransformationException(
                $"Lookup failed for target '{mapping.Target}': value '{key}' has no matching entry."),
            "null" => LogLookupMissAndReturnNull(mapping.Target, key),
            _ => value
        };
    }

    private JsonNode? LogLookupMissAndReturnNull(string target, string key)
    {
        _logger.LogWarning("Lookup miss for target '{Target}': value '{Key}' has no matching entry — writing null.", target, key);
        return null;
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

    private JsonNode? ResolveCondition(ConditionConfig condition, JsonNode input)
    {
        var branch = !string.IsNullOrEmpty(condition.If) && _conditionEvaluator.Evaluate(condition.If, input)
            ? condition.Then
            : condition.Else;

        if (!branch.HasValue || branch.Value.ValueKind == JsonValueKind.Undefined)
            return null;

        // JSONPath branch — string values starting with "$" are resolved against input
        if (branch.Value.ValueKind == JsonValueKind.String)
        {
            var str = branch.Value.GetString()!;
            if (str.StartsWith('$'))
            {
                var path = JsonPath.Parse(str);
                var match = path.Evaluate(input).Matches.FirstOrDefault();
                return match?.Value;
            }
        }

        return JsonNode.Parse(branch.Value.GetRawText());
    }

    private JsonNode? ApplyArrayMapping(MappingConfig mapping, JsonObject input, TransformConfig config, string mismatchMode)
    {
        if (string.IsNullOrEmpty(mapping.Source))
            return new JsonArray();

        var (found, sourceNode) = ResolveSource(mapping.Source, input);

        if (!found || sourceNode is null)
        {
            _logger.LogWarning("Array source '{Source}' matched no value — writing empty array.", mapping.Source);
            return new JsonArray();
        }

        if (sourceNode is not JsonArray sourceArray)
        {
            return mismatchMode.ToLowerInvariant() switch
            {
                "error" => throw new TransformationException(
                    $"Type mismatch on target '{mapping.Target}': expected array, got non-array value."),
                "null" => null,
                _ => LogCoercionAndReturnNull(mapping.Target, "expected array, got non-array value")
            };
        }

        var itemMappings = BuildItemMappings(mapping.ItemMapping!);
        var itemConfig = new TransformConfig
        {
            Mappings = itemMappings,
            Settings = config.Settings,
            ErrorHandling = config.ErrorHandling
        };

        var result = new JsonArray();
        foreach (var item in sourceArray)
        {
            if (item is not JsonObject itemObj)
                throw new TransformationException(
                    $"Array item in '{mapping.Source}' is not a JSON object and cannot be mapped with itemMapping.");

            result.Add(Transform(itemObj, itemConfig));
        }

        return result;
    }

    private static List<MappingConfig> BuildItemMappings(Dictionary<string, JsonElement> itemMapping)
    {
        var mappings = new List<MappingConfig>(itemMapping.Count);

        foreach (var (key, element) in itemMapping)
        {
            MappingConfig mc;

            if (element.ValueKind == JsonValueKind.String)
            {
                mc = new MappingConfig { Source = element.GetString() };
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                mc = JsonSerializer.Deserialize<MappingConfig>(element.GetRawText(), ItemMappingDeserializeOptions)!;
            }
            else
            {
                continue;
            }

            mc.Target = key;
            mappings.Add(mc);
        }

        return mappings;
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
