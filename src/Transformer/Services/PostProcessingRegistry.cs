using Transformer.Exceptions;
using Transformer.Services.PostProcessing;

namespace Transformer.Services;

public class PostProcessingRegistry
{
    private readonly Dictionary<string, IPostProcessingStep> _steps;

    public PostProcessingRegistry(IEnumerable<(string Name, IPostProcessingStep Step)> registrations)
    {
        _steps = registrations.ToDictionary(r => r.Name, r => r.Step, StringComparer.OrdinalIgnoreCase);
    }

    public IPostProcessingStep Resolve(string type)
    {
        if (_steps.TryGetValue(type, out var step))
            return step;

        throw new TransformationException($"Unknown post-processing step type '{type}'.");
    }
}
