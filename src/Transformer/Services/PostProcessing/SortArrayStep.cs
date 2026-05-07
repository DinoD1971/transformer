using System.Text.Json.Nodes;
using Transformer.Models;

namespace Transformer.Services.PostProcessing;

public class SortArrayStep : IPostProcessingStep
{
    public void Execute(JsonObject output, PostProcessingConfig step)
    {
        if (string.IsNullOrEmpty(step.Target) || string.IsNullOrEmpty(step.By))
            return;

        var array = GetAtPath(output, step.Target) as JsonArray;
        if (array is null)
            return;

        var sorted = array
            .OrderBy(item => GetSortKey(item, step.By))
            .Select(item => item?.DeepClone())
            .ToList();

        SetAtPath(output, step.Target, new JsonArray([.. sorted]));
    }

    private static string GetSortKey(JsonNode? item, string by)
    {
        var field = item?[by];
        if (field is null) return string.Empty;
        if (field is JsonValue jv && jv.TryGetValue<string>(out var s)) return s;
        return field.ToJsonString();
    }

    private static JsonNode? GetAtPath(JsonObject root, string dotPath)
    {
        var parts = dotPath.Split('.');
        JsonNode? current = root;
        foreach (var part in parts)
        {
            if (current is not JsonObject obj) return null;
            current = obj[part];
        }
        return current;
    }

    private static void SetAtPath(JsonObject root, string dotPath, JsonNode? value)
    {
        var parts = dotPath.Split('.');
        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!current.TryGetPropertyValue(parts[i], out var next) || next is not JsonObject child)
                return;
            current = child;
        }
        current[parts[^1]] = value;
    }
}
