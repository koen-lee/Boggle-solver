```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-NFJNRZ : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method            | Mean       | Error     | StdDev   | Gen0     | Gen1    | Gen2    | Allocated  |
|------------------ |-----------:|----------:|---------:|---------:|--------:|--------:|-----------:|
| RandomOrderWrites | 5,974.0 μs |  58.60 μs | 34.87 μs |  85.9375 |  7.8125 |       - |  716.82 KB |
| SequentialWrites  | 3,368.1 μs |  35.84 μs | 23.70 μs | 101.5625 |  7.8125 |       - |  846.43 KB |
| GuidWrites        | 2,417.2 μs |  39.56 μs | 26.17 μs | 105.4688 | 11.7188 |       - |  886.71 KB |
| ReadAllKeys       |   460.8 μs |   6.39 μs |  4.22 μs |  16.1133 |       - |       - |  134.25 KB |
| OverflowScenario  | 4,026.7 μs | 118.05 μs | 78.08 μs | 117.1875 | 39.0625 | 23.4375 | 1052.28 KB |
