```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-HOSYXM : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 3,032.38 μs | 644.314 μs | 426.174 μs |  3.9063 |       - |       - |   32818 B |
| RandomOrderWrites_Dictionary |    94.38 μs |  21.152 μs |  13.991 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 1,219.64 μs |  48.576 μs |  28.907 μs |  3.9063 |       - |       - |   32817 B |
| GuidWrites                   | 1,130.54 μs |  19.387 μs |  11.537 μs |  3.9063 |       - |       - |   32817 B |
| FileWrites                   |   634.66 μs |  18.490 μs |  11.003 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   426.57 μs |   8.656 μs |   5.151 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.40 μs |   0.825 μs |   0.545 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   146.17 μs |   3.042 μs |   2.012 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   337.86 μs |   6.762 μs |   4.472 μs |       - |       - |       - |         - |
| OverflowScenario             | 1,857.28 μs | 139.953 μs |  83.284 μs | 29.2969 | 29.2969 | 29.2969 |  334442 B |
