namespace Transformer.Exceptions;

public class ConfigNotFoundException : Exception
{
    public string Domain { get; }
    public string Operation { get; }
    public string ConfigName { get; }

    public ConfigNotFoundException(string domain, string operation, string configName)
        : base($"No config found for {domain}/{operation}/{configName}")
    {
        Domain = domain;
        Operation = operation;
        ConfigName = configName;
    }
}
