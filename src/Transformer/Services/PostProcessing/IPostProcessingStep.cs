using System.Text.Json.Nodes;
using Transformer.Models;

namespace Transformer.Services.PostProcessing;

public interface IPostProcessingStep
{
    void Execute(JsonObject output, PostProcessingConfig step);
}
