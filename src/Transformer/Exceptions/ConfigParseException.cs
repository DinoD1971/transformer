namespace Transformer.Exceptions;

public class ConfigParseException : Exception
{
    public string Domain { get; }
    public string Operation { get; }
    public string ConfigName { get; }

    public ConfigParseException(string domain, string operation, string configName, string detail, Exception? inner = null)
        : base($"Failed to parse config {domain}/{operation}/{configName}: {detail}", inner)
    {
        Domain = domain;
        Operation = operation;
        ConfigName = configName;
    }
}
