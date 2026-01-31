```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-WRHQAW : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error     | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|----------:|----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 1,316.16 μs | 39.243 μs | 25.957 μs |  3.9063 |       - |       - |   32817 B |
| RandomOrderWrites_Dictionary |    62.81 μs |  4.756 μs |  3.146 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             |   843.04 μs | 32.087 μs | 21.224 μs |  3.9063 |       - |       - |   32816 B |
| GuidWrites                   |   855.59 μs | 20.289 μs | 13.420 μs |  3.9063 |       - |       - |   32816 B |
| FileWrites                   |   520.14 μs |  9.905 μs |  5.895 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   395.93 μs |  8.512 μs |  5.630 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.63 μs |  0.774 μs |  0.512 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   148.32 μs |  3.446 μs |  2.279 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   341.19 μs |  7.458 μs |  4.933 μs |       - |       - |       - |         - |
| OverflowScenario             | 1,465.83 μs | 51.955 μs | 30.917 μs | 29.2969 | 29.2969 | 29.2969 |  334442 B |
