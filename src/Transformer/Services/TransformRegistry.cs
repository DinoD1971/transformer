using Transformer.Exceptions;
using Transformer.Services.TransformFunctions;

namespace Transformer.Services;

public class TransformRegistry
{
    private readonly Dictionary<string, ITransformFunction> _functions;

    public TransformRegistry(IEnumerable<(string Name, ITransformFunction Function)> registrations)
    {
        _functions = registrations.ToDictionary(r => r.Name, r => r.Function, StringComparer.OrdinalIgnoreCase);
    }

    public ITransformFunction Resolve(string name)
    {
        if (_functions.TryGetValue(name, out var fn))
            return fn;

        throw new TransformationException($"Unknown transform function '{name}'.");
    }
}
