using Staticsoft.GraphOperations.Abstractions;
using System.Text.Json;

namespace Staticsoft.GraphOperations.Memory;

public class JsonGraphProcessor : GraphProcessor
{
    readonly ObjectGraphProcessorFactory Factory;

    public JsonGraphProcessor(ObjectGraphProcessorFactory factory)
        => Factory = factory;

    public Task<object> Process(string serializedGraph)
    {
        var graph = Deserialize(serializedGraph);
        var processor = Factory.Create(graph);
        return processor.Process();
    }

    static Dictionary<string, object> Deserialize(string serializedTree)
    {
        var tree = JsonDocument.Parse(serializedTree).RootElement;
        if (tree.ValueKind != JsonValueKind.Object) throw new FormatException();

        return (Deserialize(tree) as Dictionary<string, object>)!;
    }

    static object Deserialize(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.False => false,
        JsonValueKind.True => true,
        JsonValueKind.Number => ParseNumber(node),
        JsonValueKind.String => node.GetString()!,
        JsonValueKind.Array => node.EnumerateArray().Select(Deserialize).ToArray(),
        JsonValueKind.Object => node.EnumerateObject().ToDictionary(property => property.Name, property => Deserialize(property.Value)),
        _ => throw new FormatException($"Unsupported {nameof(JsonValueKind)} '{node.ValueKind}'")
    };

    static object ParseNumber(JsonElement node)
        => node.TryGetInt32(out var number)
        ? number
        : node.GetDouble();
}
