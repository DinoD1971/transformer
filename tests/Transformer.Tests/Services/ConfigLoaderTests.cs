using Microsoft.Extensions.Logging;
using Moq;
using Transformer.Exceptions;
using Transformer.Services;

namespace Transformer.Tests.Services;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigLoader _loader;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        var logger = new Mock<ILogger<ConfigLoader>>();
        _loader = new ConfigLoader(logger.Object, _tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void WriteConfig(string domain, string operation, string configName, string json)
    {
        var dir = Path.Combine(_tempDir, domain, operation);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{configName}.json"), json);
    }

    [Fact]
    public async Task LoadAsync_ValidConfig_ReturnsParsedModel()
    {
        WriteConfig("crm", "order", "test", """{"version":"1.0","description":"Test config"}""");

        var config = await _loader.LoadAsync("crm", "order", "test");

        Assert.Equal("1.0", config.Version);
        Assert.Equal("Test config", config.Description);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsConfigNotFoundException()
    {
        await Assert.ThrowsAsync<ConfigNotFoundException>(() =>
            _loader.LoadAsync("crm", "order", "nonexistent"));
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ThrowsConfigParseException()
    {
        WriteConfig("crm", "order", "bad", "{ this is not valid json }");

        await Assert.ThrowsAsync<ConfigParseException>(() =>
            _loader.LoadAsync("crm", "order", "bad"));
    }

    [Fact]
    public async Task LoadAsync_CalledTwice_ParsesFileOnce()
    {
        var filePath = Path.Combine(_tempDir, "crm", "order");
        Directory.CreateDirectory(filePath);
        var fullPath = Path.Combine(filePath, "cached.json");
        File.WriteAllText(fullPath, """{"version":"1.0"}""");

        var first = await _loader.LoadAsync("crm", "order", "cached");
        File.Delete(fullPath); // remove the file; second call must use cache

        var second = await _loader.LoadAsync("crm", "order", "cached");

        Assert.Same(first, second);
    }
}
