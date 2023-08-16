namespace Staticsoft.GraphOperations.Abstractions;

public interface GraphProcessor
{
    Task<object> Process(string serializedGraph);
}

public class GraphProcessorException : Exception
{
    public GraphProcessorException(string message)
        : base(message) { }

    public GraphProcessorException(string message, Exception innerException)
        : base(message, innerException) { }
}