using BenchmarkDotNet.Running;

namespace Valkey.Benchmarks;

/// <summary>
/// Benchmark runner program.
///
/// Prerequisites:
/// 1. Start a Valkey/Redis server:
///    docker run -p 6379:6379 valkey/valkey:8
///
/// 2. Run benchmarks:
///    dotnet run -c Release --project tests/Valkey.Benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Valkey.NET Benchmarks");
        Console.WriteLine("===================");
        Console.WriteLine();
        Console.WriteLine("Comparing Valkey.NET vs StackExchange.Redis");
        Console.WriteLine("Make sure Valkey/Redis is running on localhost:6379");
        Console.WriteLine();

        var summary = BenchmarkRunner.Run<RedisComparison>();
    }
}
