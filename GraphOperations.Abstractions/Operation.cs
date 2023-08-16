namespace Staticsoft.GraphOperations.Abstractions;

public interface Operation
{
    string Type { get; }
    Task<object> Process(object properties);
    Type PropertiesType { get; }
}

public abstract class Operation<Properties, Result> : Operation
    where Result : OperationResult
{
    public string Type
        => GetType().Name.Replace(nameof(Operation), string.Empty);

    public Type PropertiesType
        => typeof(Properties);

    public async Task<object> Process(object properties)
        => await Process((Properties)properties);

    protected abstract Task<Result> Process(Properties properties);
}
