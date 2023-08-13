using Staticsoft.GraphOperations.Abstractions;

namespace Staticsoft.GraphOperations.Memory;

public class ObjectGraphProcessorFactory
{
    readonly IEnumerable<Operation> Operations;
    readonly ObjectSerializer Serializer;

    public ObjectGraphProcessorFactory(
        IEnumerable<Operation> operations,
        ObjectSerializer serializer
    )
        => (Operations, Serializer)
        = (operations, serializer);


    public ObjectGraphProcessor Create(Dictionary<string, object> graph)
        => new(Operations, Serializer, graph);
}
