```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-ATKMQW : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 3,157.44 μs | 348.700 μs | 230.644 μs |  3.9063 |       - |       - |   32818 B |
| RandomOrderWrites_Dictionary |    77.17 μs |  19.341 μs |  12.793 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 1,198.81 μs |  36.952 μs |  24.441 μs |  3.9063 |       - |       - |   32817 B |
| GuidWrites                   | 1,114.72 μs |  21.204 μs |  12.618 μs |  3.9063 |       - |       - |   32817 B |
| FileWrites                   |   626.72 μs |   9.958 μs |   5.926 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   400.48 μs |  11.847 μs |   7.836 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.54 μs |   0.736 μs |   0.438 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   146.15 μs |   4.002 μs |   2.647 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |          NA |         NA |         NA |      NA |      NA |      NA |        NA |
| OverflowScenario             | 1,824.93 μs |  37.632 μs |  22.394 μs | 29.2969 | 29.2969 | 29.2969 |  334442 B |

Benchmarks with issues:
  FlatTrieBenchmarks.ReadAllFileKeys: Job-ATKMQW(IterationCount=10, WarmupCount=3)
