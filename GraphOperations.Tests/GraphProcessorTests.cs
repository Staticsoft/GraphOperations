using Microsoft.Extensions.DependencyInjection;
using Staticsoft.GraphOperations.Abstractions;
using Staticsoft.GraphOperations.Memory;
using Staticsoft.Testing;
using System.Text.Json;
using Xunit;

namespace Staticsoft.GraphOperations.Tests;

public class GraphProcessorTests : TestBase<GraphProcessor>
{
    protected override IServiceCollection Services => base.Services
        .UseJsonGraphProcessor(graph => graph
            .With<SquareOperation>()
            .With<SumOperation>()
            .With<SleepOneSecondAndSquareOperation>()
        );

    [Test]
    public async Task ProcessesSingleOperation()
    {
        var result = await Process(new
        {
            Single = new
            {
                Type = "Square",
                Properties = new
                {
                    Input = 2
                }
            }
        });
        Assert.Equal(4, result.Output);
    }

    [Test]
    public async Task ProcessesMultipleOperationsUsingGetAtt()
    {
        var result = await Process(new
        {
            First = new
            {
                Type = "Square",
                Properties = new
                {
                    Input = 2
                }
            },
            Second = new
            {
                Type = "Square",
                Properties = new
                {
                    Input = GetAtt("First", "Output")
                }
            }
        });
        Assert.Equal(16, result.Output);
    }

    [Test]
    public async Task ProcessesMultipleOperationsUsingRef()
    {
        var result = await Process(new
        {
            First = new
            {
                Type = "Square",
                Properties = new
                {
                    Input = 2
                }
            },
            Second = new
            {
                Type = "Square",
                Properties = new
                {
                    Input = Ref("First")
                }
            }
        });
        Assert.Equal(16, result.Output);
    }

    const int SlightlyMoreThanOneSecond = 1100;

    [Test(Timeout = SlightlyMoreThanOneSecond)]
    public async Task ProcessesOperationsInParallel()
    {
        var result = await Process(new
        {
            First = new
            {
                Type = "SleepOneSecondAndSquare",
                Properties = new
                {
                    Input = 2
                }
            },
            Second = new
            {
                Type = "SleepOneSecondAndSquare",
                Properties = new
                {
                    Input = 3
                }
            },
            Third = new
            {
                Type = "Sum",
                Properties = new
                {
                    Input = new[]
                    {
                        Ref("First"),
                        Ref("Second")
                    }
                }
            }
        });
        Assert.Equal(13, result.Output);
    }

    [Test(Skip = "Not implemented yet")]
    public async Task ProcessesSameOperationsOnlyOnce()
    {
        var result = await Process(new
        {
            First = new
            {
                Type = "SleepOneSecondAndSquare",
                Properties = new
                {
                    Input = 2
                }
            },
            Second = new
            {
                Type = "Sum",
                Properties = new
                {
                    Input = new[]
                    {
                        Ref("First"),
                        Ref("First")
                    }
                }
            }
        });
        Assert.Equal(8, result.Output);
        Assert.Equal(1, GetOperation<SleepOneSecondAndSquareOperation>().Executions);
    }

    async Task<NumberResult> Process<Configuration>(Configuration configuration)
    {
        var processed = await SUT.Process(JsonSerializer.Serialize(configuration));
        return (NumberResult)processed;
    }

    OperationType GetOperation<OperationType>()
        => Get<IEnumerable<Operation>>().OfType<OperationType>().Single();

    static object Ref(string nodeName)
        => new { Ref = nodeName };

    static object GetAtt(string nodeName, string attributeName)
        => new
        {
            GetAtt = new[]
            {
                nodeName,
                attributeName
            }
        };
}

public class NumberProperties
{
    public int Input { get; init; }
}

public class NumbersProperties
{
    public int[] Input { get; init; } = Array.Empty<int>();
}

public class NumberResult : OperationResult
{
    public int Output { get; init; }

    public object RefAttribute
        => Output;
}

public class SquareOperation : Operation<NumberProperties, NumberResult>
{
    protected override Task<NumberResult> Process(NumberProperties properties)
        => Task.FromResult(Square(properties.Input));

    static NumberResult Square(int number)
        => new() { Output = number * number };
}

public class SleepOneSecondAndSquareOperation : Operation<NumberProperties, NumberResult>
{
    int ExecutedTimes = 0;

    public int Executions
        => ExecutedTimes;

    protected override async Task<NumberResult> Process(NumberProperties properties)
    {
        await Task.Delay(1000);
        Interlocked.Increment(ref ExecutedTimes);
        return Square(properties.Input);
    }

    static NumberResult Square(int number)
        => new() { Output = number * number };
}

public class SumOperation : Operation<NumbersProperties, NumberResult>
{
    protected override Task<NumberResult> Process(NumbersProperties properties)
        => Task.FromResult(Sum(properties.Input));

    static NumberResult Sum(int[] numbers)
        => new() { Output = numbers.Sum() };
}