```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-VGJORJ : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 4,063.69 μs |  40.947 μs |  24.367 μs |       - |       - |       - |   32819 B |
| RandomOrderWrites_Dictionary |    61.64 μs |   5.210 μs |   3.446 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 2,187.14 μs | 223.833 μs | 133.199 μs |  3.9063 |       - |       - |   32818 B |
| GuidWrites                   | 1,814.24 μs |  18.937 μs |   9.904 μs |  3.9063 |       - |       - |   32817 B |
| FileWrites                   |   836.30 μs |  12.831 μs |   8.487 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   454.17 μs |   6.542 μs |   4.327 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    22.97 μs |   0.235 μs |   0.156 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   143.15 μs |   1.437 μs |   0.855 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   335.31 μs |   5.039 μs |   3.333 μs |       - |       - |       - |         - |
| OverflowScenario             | 2,648.70 μs |  41.710 μs |  27.589 μs | 27.3438 | 27.3438 | 27.3438 |  334444 B |
