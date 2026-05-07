using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Transformer.Exceptions;
using Transformer.Models;

namespace Transformer.Services;

public class ConfigLoader : IConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;
    private readonly ILogger<ConfigLoader> _logger;
    private readonly ConcurrentDictionary<string, TransformConfig> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ConfigLoader(ILogger<ConfigLoader> logger)
        : this(logger, Path.Combine(AppContext.BaseDirectory, "Configs")) { }

    internal ConfigLoader(ILogger<ConfigLoader> logger, string basePath)
    {
        _logger = logger;
        _basePath = basePath;
    }

    public async Task<TransformConfig> LoadAsync(string domain, string operation, string configName, CancellationToken cancellationToken = default)
    {
        var key = $"{domain}/{operation}/{configName}";

        if (_cache.TryGetValue(key, out var cached))
        {
            _logger.LogInformation("Config cache hit. Key={Key}", key);
            return cached;
        }

        var path = Path.Combine(_basePath, domain, operation, $"{configName}.json");

        if (!File.Exists(path))
            throw new ConfigNotFoundException(domain, operation, configName);

        _logger.LogInformation("Loading config from disk. Key={Key} Path={Path}", key, path);

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new ConfigParseException(domain, operation, configName, ex.Message, ex);
        }

        TransformConfig config;
        try
        {
            config = JsonSerializer.Deserialize<TransformConfig>(json, SerializerOptions)
                ?? throw new ConfigParseException(domain, operation, configName, "Config deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new ConfigParseException(domain, operation, configName, ex.Message, ex);
        }

        _cache.TryAdd(key, config);
        return config;
    }
}
