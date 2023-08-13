using System.Text.Json;
using System.Text.Json.Serialization;

namespace Staticsoft.GraphOperations.Memory;

public class ObjectSerializer
{
    readonly JsonSerializerOptions Options;

    public ObjectSerializer()
    {
        Options = new() { PropertyNameCaseInsensitive = true };
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public object ToType(object obj, Type targetType)
        => JsonSerializer.Deserialize(JsonSerializer.Serialize(obj), targetType, Options)
        ?? throw new InvalidCastException($"Cannot convert object of type '{obj.GetType().Name}' to type '{targetType.Name}'");
}