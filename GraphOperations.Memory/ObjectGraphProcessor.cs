using Staticsoft.GraphOperations.Abstractions;

namespace Staticsoft.GraphOperations.Memory;

public class ObjectGraphProcessor
{
    readonly IEnumerable<Operation> Operations;
    readonly ObjectSerializer Serializer;
    readonly Dictionary<string, object> Graph;

    public ObjectGraphProcessor(
        IEnumerable<Operation> operations,
        ObjectSerializer serializer,
        Dictionary<string, object> graph
    )
        => (Operations, Serializer, Graph)
        = (operations, serializer, graph);

    public Task<object> Process()
    {
        var root = FindRoot();
        return ProcessNode(root);
    }

    object FindRoot()
    {
        var references = new HashSet<string>();
        foreach (var nodeName in Graph.Keys)
        {
            var nodeReferences = FindReferences(Graph[nodeName]);
            foreach (var reference in nodeReferences)
            {
                references.Add(reference);
            }
        }

        return Try
            .Return(() => Graph[GetRootKey(references)])
            .On<InvalidOperationException>(Exception("Unable to find root operation"))
            .Result();
    }

    string GetRootKey(HashSet<string> references)
        => Graph.Keys.Single(nodeName => !references.Contains(nodeName));

    IEnumerable<string> FindReferences(object node)
    {
        if (node is Dictionary<string, object> dictionary)
        {
            foreach (var reference in dictionary.Values.SelectMany(FindReferences))
            {
                yield return reference;
            }

            if (dictionary.ContainsKey("Ref"))
            {
                if (dictionary["Ref"] is not string reference)
                {
                    throw Error($"A node has a `Ref` field with unexpected type: {dictionary["Ref"].GetType().FullName}. A string was expected");
                }

                yield return reference;
            }

            if (dictionary.ContainsKey("GetAtt"))
            {
                if (dictionary["GetAtt"] is not object[] attributeReference)
                {
                    throw Error($"A node has a `GetAtt` field with unexpected type: {dictionary["GetAtt"].GetType().FullName}. A string array was expected");
                }
                if (attributeReference.Length != 2)
                {
                    throw Error($"A node has a `GetAtt` field with unexepcted amount of elements: {attributeReference.Length}. An array was expected to have 2 elements");
                }

                var reference = attributeReference.First() as string;

                yield return reference ?? throw Error($"A node has a `GetAtt` field with unexpected type of the first element: {attributeReference.First().GetType().FullName}. A string was expected");
            }
        }
        if (node is object[] array)
        {
            foreach (var reference in array.SelectMany(FindReferences))
            {
                yield return reference;
            }
        }
    }

    async Task<object> ProcessNode(object node)
    {
        if (node is object[] array) return await ProcessArray(array);
        if (node is not Dictionary<string, object> unprocessed) return node;

        var dictionary = await ProcessPropertyValues(unprocessed);

        if (IsReference(dictionary)) return await ProcessReference(dictionary);
        if (IsOperation(dictionary)) return await ProcessOperation(dictionary);

        return dictionary;
    }

    Task<object[]> ProcessArray(object[] array)
        => Task.WhenAll(array.Select(ProcessNode));

    async Task<Dictionary<string, object>> ProcessPropertyValues(Dictionary<string, object> dictionary)
    {
        var processed = await Task.WhenAll(dictionary.Keys.Select(async key =>
        {
            var value = await ProcessNode(dictionary[key]);
            return new { Key = key, Value = value };
        }));
        return processed.ToDictionary(item => item.Key, item => item.Value);
    }

    static bool IsReference(Dictionary<string, object> dictionary)
        => dictionary.ContainsKey("Ref") || dictionary.ContainsKey("GetAtt");

    static bool IsOperation(Dictionary<string, object> dictionary)
        => dictionary.ContainsKey("Type") && dictionary.ContainsKey("Properties");

    async Task<object> ProcessReference(Dictionary<string, object> dictionary)
    {
        if (dictionary.ContainsKey("Ref"))
        {
            if (dictionary["Ref"] is not string reference)
            {
                throw Error($"A node has a `Ref` field with unexpected type: {dictionary["Ref"].GetType().FullName}. A string was expected");
            }
            return await ProcessReference(reference, nameof(OperationResult.RefAttribute));
        }
        if (dictionary.ContainsKey("GetAtt"))
        {
            if (dictionary["GetAtt"] is not object[] attributeReference)
            {
                throw Error($"A node has a `GetAtt` field with unexpected type: {dictionary["GetAtt"].GetType().FullName}. A string array was expected");
            }
            if (attributeReference.Length != 2)
            {
                throw Error($"A node has a `GetAtt` field with unexepcted amount of elements: {attributeReference.Length}. An array was expected to have 2 elements");
            }
            var reference = attributeReference.First() as string ?? throw Error($"A node has a `GetAtt` field with unexpected type of the first element: {attributeReference.First().GetType().FullName}. A string was expected");
            var attribute = attributeReference.Last() as string ?? throw Error($"A node has a `GetAtt` field with unexpected type of the second element: {attributeReference.First().GetType().FullName}. A string was expected");
            return await ProcessReference(reference, attribute);
        }
        throw new NotSupportedException($"A node with keys [{string.Join(", ", dictionary.Keys)}] was recognized as a reference, but no supported reference keys were found");
    }

    async Task<object> ProcessReference(string reference, string attribute)
    {
        var processed = await ProcessNode(Graph[reference]);
        var property = processed.GetType().GetProperty(attribute) ?? throw Error($"An object of type {processed.GetType().FullName} was expected to have property '{attribute}', but there was no such property");
        return property.GetValue(processed) ?? throw Error($"An object of type {processed.GetType().FullName} was expected to return non-nullable value when property '{attribute}' value was retrieved, but null was found");
    }

    async Task<object> ProcessOperation(Dictionary<string, object> dictionary)
    {
        var operationName = GetNodeType(dictionary);
        var operationData = GetNodeProperties(dictionary);
        var operation = Operations.Single(operation => operation.Type == operationName);
        var data = Serializer.ToType(operationData, operation.PropertiesType);
        return await operation.Process(data) ?? throw Error($"An operation of type {operation.GetType().FullName} was expected to return non-nullable value when processing object of type {data.GetType().FullName}, but null was found");
    }

    static string GetNodeType(Dictionary<string, object> node)
        => node["Type"] as string ?? throw Error($"A node was expected to have 'Type' key, but only these keys were found: [{string.Join(", ", node.Keys)}]");

    static object GetNodeProperties(Dictionary<string, object> node)
        => node["Properties"];

    static Func<Exception, Exception> Exception(string error)
        => (exception) => new GraphProcessorException(error, exception);

    static GraphProcessorException Error(string error)
        => new(error);
}