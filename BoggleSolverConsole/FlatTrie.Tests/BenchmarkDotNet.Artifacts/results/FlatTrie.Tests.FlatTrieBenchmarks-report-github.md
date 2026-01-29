```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-XTNPDO : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method            | Mean       | Error    | StdDev   | Gen0     | Gen1    | Gen2    | Allocated  |
|------------------ |-----------:|---------:|---------:|---------:|--------:|--------:|-----------:|
| RandomOrderWrites | 6,017.3 μs | 94.81 μs | 62.71 μs |  85.9375 |  7.8125 |       - |  716.82 KB |
| SequentialWrites  | 3,316.4 μs | 36.64 μs | 24.23 μs | 101.5625 |  7.8125 |       - |  846.43 KB |
| GuidWrites        | 2,410.7 μs | 35.08 μs | 20.88 μs | 105.4688 | 11.7188 |       - |  889.09 KB |
| ReadAllKeys       |   168.0 μs |  1.88 μs |  1.24 μs |   9.7656 |       - |       - |   80.28 KB |
| ReadAllGuidKeys   |   170.4 μs |  3.29 μs |  2.18 μs |   9.7656 |       - |       - |   80.28 KB |
| OverflowScenario  | 3,837.9 μs | 52.64 μs | 34.82 μs | 117.1875 | 39.0625 | 27.3438 | 1052.28 KB |
