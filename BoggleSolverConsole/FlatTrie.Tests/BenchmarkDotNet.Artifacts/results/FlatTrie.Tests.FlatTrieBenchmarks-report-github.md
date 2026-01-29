```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-ZEMMQE : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 6,215.18 μs | 413.163 μs | 273.281 μs | 62.5000 |       - |       - |  584251 B |
| RandomOrderWrites_Dictionary |    64.44 μs |   3.064 μs |   2.026 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 3,256.50 μs |  50.304 μs |  26.310 μs | 82.0313 |  7.8125 |       - |  716978 B |
| GuidWrites                   | 2,431.13 μs |  76.506 μs |  45.527 μs | 78.1250 |  7.8125 |       - |  660002 B |
| FileWrites                   | 1,618.99 μs |  43.668 μs |  25.986 μs | 85.9375 |  7.8125 |       - |  727369 B |
| ReadAllKeys                  |   411.90 μs |   7.656 μs |   5.064 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    24.51 μs |   0.660 μs |   0.436 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   150.25 μs |   2.900 μs |   1.918 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   348.03 μs |   3.735 μs |   2.223 μs |       - |       - |       - |         - |
| OverflowScenario             | 3,899.65 μs |  85.078 μs |  56.274 μs | 85.9375 | 39.0625 | 23.4375 |  802516 B |
