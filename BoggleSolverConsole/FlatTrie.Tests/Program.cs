using BenchmarkDotNet.Running;
using Xunit.Abstractions;

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
                case "trie":
                default:
                    BenchmarkRunner.Run<FlatTrieBenchmarks>();
                    break;
            }
        }
        else
        {
            Console.WriteLine("Run with --benchmark [trie|shift|threshold] to execute benchmarks");
            Console.WriteLine("Or use 'dotnet test' to run unit tests");

            var trie = new FlatTrie();
            
            int keyCount = 2146;
            var keys = new string[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                keys[i] = $"k{i:d6}";
                if (!trie.TryWrite(keys[i], i))
                {
                    keyCount = i - 1;
                    break;
                }
            }

            for( int j = 0; j < 1_000; j++)
            {
                for (int i = 0; i < keyCount; i++)
                {
                    if (!trie.TryRead(keys[i], out var value) )
                        throw new InvalidDataException("key not found "+i);
                    if( value != i )
                        throw new InvalidDataException($"value {value} wrong, should be" + i);
                }
            }
             
        }
    }

    class ConsoleOutputHelper : ITestOutputHelper
    {
        public void WriteLine(string message)
        {
            WriteLine(message, []);
        }

        public void WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}
