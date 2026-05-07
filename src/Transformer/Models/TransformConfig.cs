namespace Transformer.Models;

public class TransformConfig
{
    public string? Version { get; set; }
    public string? Description { get; set; }
    public List<MappingConfig> Mappings { get; set; } = [];
}
