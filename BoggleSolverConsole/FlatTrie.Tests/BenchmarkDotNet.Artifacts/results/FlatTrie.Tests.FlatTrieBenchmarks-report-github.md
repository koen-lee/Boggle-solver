```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-XAWOVQ : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|---------:|---------:|--------:|----------:|
| RandomOrderWrites            | 5,770.11 μs | 260.485 μs | 155.010 μs |        - |        - |       - |   32819 B |
| RandomOrderWrites_Dictionary |   102.39 μs |  11.768 μs |   7.003 μs | 156.8604 | 156.8604 | 26.2451 |  215649 B |
| SequentialWrites             | 2,116.54 μs |  41.268 μs |  27.296 μs |   3.9063 |        - |       - |   32818 B |
| GuidWrites                   | 1,789.92 μs |  35.347 μs |  23.380 μs |   3.9063 |        - |       - |   32817 B |
| FileWrites                   |   842.59 μs |  16.712 μs |  11.054 μs |   3.9063 |        - |       - |   32816 B |
| ReadAllKeys                  |   391.43 μs |   6.802 μs |   4.048 μs |        - |        - |       - |         - |
| ReadAllKeys_Dictionary       |    22.85 μs |   0.434 μs |   0.258 μs |        - |        - |       - |         - |
| ReadAllGuidKeys              |   142.46 μs |   1.486 μs |   0.884 μs |        - |        - |       - |         - |
| ReadAllFileKeys              |   337.13 μs |   3.007 μs |   1.789 μs |        - |        - |       - |         - |
| OverflowScenario             | 2,980.33 μs |  36.999 μs |  24.472 μs |  27.3438 |  27.3438 | 27.3438 |  334444 B |
