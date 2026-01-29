```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-VZGCLI : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 6,094.31 μs | 139.777 μs | 92.454 μs | 62.5000 |       - |       - |  584251 B |
| RandomOrderWrites_Dictionary |    60.82 μs |   1.220 μs |  0.726 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 3,331.26 μs |  35.942 μs | 18.799 μs | 82.0313 |  7.8125 |       - |  716978 B |
| GuidWrites                   | 2,563.64 μs |  33.834 μs | 22.379 μs | 74.2188 |  7.8125 |       - |  649042 B |
| FileWrites                   | 1,643.72 μs |  22.934 μs | 15.169 μs | 85.9375 |  7.8125 |       - |  727737 B |
| ReadAllKeys                  |   407.68 μs |   4.169 μs |  2.758 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.54 μs |   0.334 μs |  0.199 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   155.57 μs |   1.994 μs |  1.319 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   345.54 μs |   2.754 μs |  1.639 μs |       - |       - |       - |         - |
| OverflowScenario             | 3,893.78 μs |  49.341 μs | 32.636 μs | 85.9375 | 39.0625 | 23.4375 |  802516 B |
