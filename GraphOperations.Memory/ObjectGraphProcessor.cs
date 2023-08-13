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

        try
        {
            var rootKey = Graph.Keys.Single(nodeName => !references.Contains(nodeName));
            return Graph[rootKey];
        }
        catch (InvalidOperationException)
        {
            throw new DecideLaterException();
        }
    }

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
                    throw new DecideLaterException();
                }

                yield return reference;
            }

            if (dictionary.ContainsKey("GetAtt"))
            {
                if (dictionary["GetAtt"] is not object[] attributeReference || attributeReference.Length != 2)
                {
                    throw new DecideLaterException();
                }

                var reference = attributeReference.First() as string;

                yield return reference ?? throw new DecideLaterException();
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
        => Task.WhenAll(array.Select(item => ProcessNode(item)));

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
                throw new DecideLaterException();
            }
            return await ProcessReference(reference, nameof(OperationResult.RefAttribute));
        }
        if (dictionary.ContainsKey("GetAtt"))
        {
            if (dictionary["GetAtt"] is not object[] attributeReference || attributeReference.Length != 2)
            {
                throw new DecideLaterException();
            }
            var reference = attributeReference.First() as string ?? throw new DecideLaterException();
            var attribute = attributeReference.Last() as string ?? throw new DecideLaterException();
            return await ProcessReference(reference, attribute);
        }
        throw new DecideLaterException();
    }

    async Task<object> ProcessReference(string reference, string attribute)
    {
        var processed = await ProcessNode(Graph[reference]);
        var property = processed.GetType().GetProperty(attribute) ?? throw new DecideLaterException();
        return property.GetValue(processed) ?? throw new DecideLaterException();
    }

    async Task<object> ProcessOperation(Dictionary<string, object> dictionary)
    {
        var operationName = GetNodeType(dictionary);
        var operationData = GetNodeProperties(dictionary);
        var operation = Operations.Single(operation => operation.Type == operationName);
        var data = Serializer.ToType(operationData, operation.PropertiesType);
        return await operation.Process(data) ?? throw new DecideLaterException();
    }

    static string GetNodeType(Dictionary<string, object> node)
        => node["Type"] as string ?? throw new DecideLaterException();

    static object GetNodeProperties(Dictionary<string, object> node)
        => node["Properties"];
}

public class DecideLaterException : Exception { }
