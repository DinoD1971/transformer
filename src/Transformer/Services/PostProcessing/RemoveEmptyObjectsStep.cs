using System.Text.Json.Nodes;
using Transformer.Models;

namespace Transformer.Services.PostProcessing;

public class RemoveEmptyObjectsStep : IPostProcessingStep
{
    public void Execute(JsonObject output, PostProcessingConfig step) =>
        RemoveEmpty(output);

    private static void RemoveEmpty(JsonObject obj)
    {
        foreach (var kv in obj)
        {
            if (kv.Value is JsonObject child)
                RemoveEmpty(child);
        }

        var emptyKeys = obj
            .Where(kv => kv.Value is JsonObject childObj && IsEmpty(childObj))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in emptyKeys)
            obj.Remove(key);
    }

    private static bool IsEmpty(JsonObject obj)
    {
        foreach (var kv in obj)
        {
            if (kv.Value is null) continue;
            if (kv.Value is JsonObject childObj && IsEmpty(childObj)) continue;
            return false;
        }
        return true;
    }
}
