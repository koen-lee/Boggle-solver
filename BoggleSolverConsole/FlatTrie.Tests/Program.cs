using BenchmarkDotNet.Running;

namespace FlatTrie.Tests;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--benchmark")
        {
            var benchmarkArg = args.Length > 1 ? args[1] : "trie";
            switch (benchmarkArg.ToLower())
            {
                case "shift":
                    BenchmarkRunner.Run<ShiftBitsRightBenchmarks>();
                    break;
                case "trie":
                default:
                    BenchmarkRunner.Run<FlatTrieBenchmarks>();
                    break;
            }
        }
        else
        {
            Console.WriteLine("Run with --benchmark [trie|shift] to execute benchmarks");
            Console.WriteLine("Or use 'dotnet test' to run unit tests");
        }
    }
}
