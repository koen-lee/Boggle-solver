```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-EFMNHU : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method            | Mean       | Error     | StdDev   | Gen0     | Gen1    | Gen2    | Allocated  |
|------------------ |-----------:|----------:|---------:|---------:|--------:|--------:|-----------:|
| RandomOrderWrites | 5,989.6 μs |  65.88 μs | 39.20 μs |  85.9375 |  7.8125 |       - |  716.82 KB |
| SequentialWrites  | 3,315.7 μs |  31.96 μs | 19.02 μs | 101.5625 | 11.7188 |       - |  846.43 KB |
| ReadAllKeys       |   437.2 μs |   5.34 μs |  3.53 μs |  16.1133 |       - |       - |  134.25 KB |
| OverflowScenario  | 3,964.1 μs | 154.11 μs | 91.71 μs | 117.1875 | 39.0625 | 23.4375 | 1052.28 KB |
