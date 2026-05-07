using System.Text.Json.Nodes;
using Transformer.Models;
using Transformer.Services.PostProcessing;

namespace Transformer.Tests.Services.PostProcessing;

public class PostProcessingStepTests
{
    private static JsonObject Obj(string json) => JsonNode.Parse(json)!.AsObject();

    // --- RemoveEmptyObjectsStep ---

    [Fact]
    public void RemoveEmptyObjects_EmptyNestedObject_Removed()
    {
        var output = Obj("""{"a":"value","empty":{}}""");
        new RemoveEmptyObjectsStep().Execute(output, new PostProcessingConfig { Type = "removeEmptyObjects" });

        Assert.True(output.ContainsKey("a"));
        Assert.False(output.ContainsKey("empty"));
    }

    [Fact]
    public void RemoveEmptyObjects_NonEmptyNestedObject_Preserved()
    {
        var output = Obj("""{"a":"value","nested":{"b":"kept"}}""");
        new RemoveEmptyObjectsStep().Execute(output, new PostProcessingConfig { Type = "removeEmptyObjects" });

        Assert.True(output.ContainsKey("nested"));
        Assert.Equal("kept", output["nested"]!["b"]?.GetValue<string>());
    }

    [Fact]
    public void RemoveEmptyObjects_ObjectWithOnlyNullFields_Removed()
    {
        var output = Obj("""{"data":{"x":null,"y":null}}""");
        new RemoveEmptyObjectsStep().Execute(output, new PostProcessingConfig { Type = "removeEmptyObjects" });

        Assert.False(output.ContainsKey("data"));
    }

    [Fact]
    public void RemoveEmptyObjects_DeeplyNestedEmpty_RemovedRecursively()
    {
        var output = Obj("""{"outer":{"inner":{}}}""");
        new RemoveEmptyObjectsStep().Execute(output, new PostProcessingConfig { Type = "removeEmptyObjects" });

        Assert.False(output.ContainsKey("outer"));
    }

    [Fact]
    public void RemoveEmptyObjects_MixedObject_OnlyEmptyChildRemoved()
    {
        var output = Obj("""{"keep":{"val":1},"drop":{}}""");
        new RemoveEmptyObjectsStep().Execute(output, new PostProcessingConfig { Type = "removeEmptyObjects" });

        Assert.True(output.ContainsKey("keep"));
        Assert.False(output.ContainsKey("drop"));
    }

    // --- SortArrayStep ---

    [Fact]
    public void SortArray_SortsByByFieldAscending()
    {
        var output = Obj("""{"items":[{"name":"Zebra"},{"name":"Apple"},{"name":"Mango"}]}""");
        new SortArrayStep().Execute(output, new PostProcessingConfig { Type = "sortArray", Target = "items", By = "name" });

        var items = output["items"]!.AsArray();
        Assert.Equal("Apple", items[0]!["name"]?.GetValue<string>());
        Assert.Equal("Mango", items[1]!["name"]?.GetValue<string>());
        Assert.Equal("Zebra", items[2]!["name"]?.GetValue<string>());
    }

    [Fact]
    public void SortArray_NestedTargetPath_ResolvesCorrectly()
    {
        var output = Obj("""{"order":{"items":[{"sku":"C"},{"sku":"A"},{"sku":"B"}]}}""");
        new SortArrayStep().Execute(output, new PostProcessingConfig { Type = "sortArray", Target = "order.items", By = "sku" });

        var items = output["order"]!["items"]!.AsArray();
        Assert.Equal("A", items[0]!["sku"]?.GetValue<string>());
        Assert.Equal("B", items[1]!["sku"]?.GetValue<string>());
        Assert.Equal("C", items[2]!["sku"]?.GetValue<string>());
    }

    [Fact]
    public void SortArray_MissingTarget_NoOp()
    {
        var output = Obj("""{"other":"value"}""");
        new SortArrayStep().Execute(output, new PostProcessingConfig { Type = "sortArray", Target = "items", By = "name" });

        Assert.True(output.ContainsKey("other"));
    }
}
