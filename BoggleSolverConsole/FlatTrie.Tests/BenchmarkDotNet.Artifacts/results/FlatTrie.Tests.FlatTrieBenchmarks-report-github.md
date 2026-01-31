```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-UZHXHO : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 2,143.44 μs |  96.495 μs | 63.825 μs |  3.9063 |       - |       - |   32818 B |
| RandomOrderWrites_Dictionary |    69.56 μs |   5.310 μs |  3.512 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 1,224.87 μs |  19.028 μs | 12.586 μs |  3.9063 |       - |       - |   32817 B |
| GuidWrites                   | 1,148.33 μs |  37.361 μs | 22.233 μs |  3.9063 |       - |       - |   32817 B |
| FileWrites                   |   661.06 μs |  22.073 μs | 14.600 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   403.95 μs |  10.974 μs |  6.530 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.79 μs |   0.811 μs |  0.483 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   144.76 μs |   1.505 μs |  0.895 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   348.66 μs |  10.615 μs |  6.317 μs |       - |       - |       - |         - |
| OverflowScenario             | 1,835.68 μs | 123.901 μs | 81.953 μs | 29.2969 | 29.2969 | 29.2969 |  334442 B |
