using System.Text.Json.Nodes;
using Transformer.Models;

namespace Transformer.Services;

public interface ITransformationEngine
{
    JsonObject Transform(JsonObject input, TransformConfig config);
}
