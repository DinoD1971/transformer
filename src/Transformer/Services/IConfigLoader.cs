using Transformer.Models;

namespace Transformer.Services;

public interface IConfigLoader
{
    Task<TransformConfig> LoadAsync(string domain, string operation, string configName, CancellationToken cancellationToken = default);
}
