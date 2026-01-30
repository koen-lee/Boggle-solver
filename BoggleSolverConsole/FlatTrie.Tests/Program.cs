using BenchmarkDotNet.Running;

namespace FlatTrie.Tests;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--benchmark")
        {
            BenchmarkRunner.Run<FlatTrieBenchmarks>();
        }
        else
        {
            Console.WriteLine("Run with --benchmark to execute benchmarks");
            Console.WriteLine("Or use 'dotnet test' to run unit tests");
        }
    }
}
