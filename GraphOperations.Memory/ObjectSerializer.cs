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
        => ToType(JsonSerializer.Serialize(obj), targetType);

    object ToType(string serialized, Type targetType)
        => Try
            .Return(() => JsonSerializer.Deserialize(serialized, targetType, Options))
            .On<JsonException>((ex) => DeserializationError(serialized, targetType, ex))
            .Result() ?? DeserializationError(serialized, targetType, new ArgumentNullException());

    static InvalidCastException DeserializationError(string serialized, Type targetType, Exception inner)
        => new($"Unable to deserialize object `{serialized}` to type {targetType.Name}", inner);
}