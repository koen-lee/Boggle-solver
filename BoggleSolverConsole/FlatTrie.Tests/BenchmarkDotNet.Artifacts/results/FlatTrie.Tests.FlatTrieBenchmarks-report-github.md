```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-LBEHLE : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev    | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|----------:|---------:|---------:|--------:|----------:|
| RandomOrderWrites            | 1,319.86 μs |  50.005 μs | 29.757 μs |   3.9063 |        - |       - |   32817 B |
| RandomOrderWrites_Dictionary |    61.45 μs |   2.356 μs |  1.558 μs | 136.6577 | 136.6577 | 26.9165 |  215649 B |
| SequentialWrites             |   832.60 μs |  21.512 μs | 14.229 μs |   3.9063 |        - |       - |   32816 B |
| GuidWrites                   |   887.83 μs |  22.615 μs | 13.458 μs |   3.9063 |        - |       - |   32816 B |
| FileWrites                   |   540.43 μs |  30.949 μs | 20.471 μs |   3.9063 |        - |       - |   32816 B |
| ReadAllKeys                  |   393.71 μs |   5.410 μs |  3.220 μs |        - |        - |       - |         - |
| ReadAllKeys_Dictionary       |    24.06 μs |   1.139 μs |  0.753 μs |        - |        - |       - |         - |
| ReadAllGuidKeys              |   150.46 μs |   9.599 μs |  6.349 μs |        - |        - |       - |         - |
| ReadAllFileKeys              |   337.64 μs |   5.468 μs |  2.860 μs |        - |        - |       - |         - |
| OverflowScenario             | 1,444.50 μs | 103.278 μs | 68.312 μs | 142.5781 | 142.5781 | 25.3906 |  334438 B |
