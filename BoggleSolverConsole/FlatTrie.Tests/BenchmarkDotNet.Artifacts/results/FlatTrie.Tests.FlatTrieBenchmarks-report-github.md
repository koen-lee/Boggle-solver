```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-GXPFZV : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error     | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|----------:|----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 1,551.55 μs | 76.472 μs | 50.581 μs |  3.9063 |       - |       - |   32818 B |
| RandomOrderWrites_Dictionary |    60.77 μs |  3.465 μs |  2.292 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             |   938.28 μs | 19.577 μs | 12.949 μs |  3.9063 |       - |       - |   32816 B |
| GuidWrites                   |   925.72 μs | 42.665 μs | 28.220 μs |  3.9063 |       - |       - |   32816 B |
| FileWrites                   |   585.27 μs | 47.354 μs | 31.322 μs |  3.9063 |       - |       - |   32816 B |
| ReadAllKeys                  |   402.24 μs | 22.081 μs | 14.605 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    24.37 μs |  2.234 μs |  1.478 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   156.42 μs | 10.512 μs |  6.953 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   354.30 μs | 19.345 μs | 12.795 μs |       - |       - |       - |         - |
| OverflowScenario             | 1,531.64 μs | 44.470 μs | 23.259 μs | 29.2969 | 29.2969 | 29.2969 |  334441 B |
