```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-CHQOBN : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 7,672.17 μs | 220.982 μs | 146.166 μs |       - |       - |       - |   32822 B |
| RandomOrderWrites_Dictionary |    61.45 μs |   4.188 μs |   2.770 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 3,560.61 μs |  36.832 μs |  24.362 μs |  3.9063 |       - |       - |   32818 B |
| GuidWrites                   | 2,695.43 μs |  17.934 μs |  10.672 μs |  3.9063 |       - |       - |   32818 B |
| FileWrites                   | 1,205.37 μs |  14.373 μs |   9.507 μs |  3.9063 |       - |       - |   32817 B |
| ReadAllKeys                  |   400.99 μs |  11.098 μs |   7.340 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.70 μs |   1.394 μs |   0.922 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   142.92 μs |   1.059 μs |   0.630 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   335.50 μs |   5.719 μs |   3.783 μs |       - |       - |       - |         - |
| OverflowScenario             | 4,038.79 μs |  43.960 μs |  29.077 μs | 23.4375 | 23.4375 | 23.4375 |  334444 B |
