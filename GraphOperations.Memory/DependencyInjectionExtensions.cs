using Microsoft.Extensions.DependencyInjection;
using Staticsoft.GraphOperations.Abstractions;

namespace Staticsoft.GraphOperations.Memory;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection UseJsonGraphProcessor(
        this IServiceCollection services,
        Func<GraphProcessorBuilder, GraphProcessorBuilder> build
    )
    {
        build(new GraphProcessorBuilder(services));

        return services
            .AddSingleton<ObjectGraphProcessorFactory>()
            .AddSingleton<ObjectSerializer>()
            .AddSingleton<GraphProcessor, JsonGraphProcessor>();
    }
}

public class GraphProcessorBuilder
{
    readonly IServiceCollection Services;

    public GraphProcessorBuilder(IServiceCollection services)
        => Services = services;

    public GraphProcessorBuilder With<GraphOperation>()
        where GraphOperation : class, Operation
    {
        Services.AddSingleton<Operation, GraphOperation>();
        return this;
    }
}