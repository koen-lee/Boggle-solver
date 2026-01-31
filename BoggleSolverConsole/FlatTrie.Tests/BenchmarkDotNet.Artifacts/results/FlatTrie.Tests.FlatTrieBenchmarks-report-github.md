```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-FRNSRC : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|---------:|---------:|--------:|----------:|
| RandomOrderWrites            | 1,248.81 μs |  31.061 μs |  18.484 μs |   3.9063 |        - |       - |   32817 B |
| RandomOrderWrites_Dictionary |    67.91 μs |   3.984 μs |   2.371 μs | 156.8604 | 156.8604 | 26.2451 |  215649 B |
| SequentialWrites             |   791.82 μs |  17.349 μs |  11.476 μs |   3.9063 |        - |       - |   32816 B |
| GuidWrites                   |   770.30 μs |  11.101 μs |   6.606 μs |   3.9063 |        - |       - |   32816 B |
| FileWrites                   |   415.40 μs |   5.552 μs |   3.304 μs |   3.9063 |        - |       - |   32816 B |
| ReadAllKeys                  |   446.13 μs | 157.874 μs | 104.424 μs |        - |        - |       - |         - |
| ReadAllKeys_Dictionary       |    24.20 μs |   1.456 μs |   0.963 μs |        - |        - |       - |         - |
| ReadAllGuidKeys              |   123.57 μs |   8.814 μs |   5.830 μs |        - |        - |       - |         - |
| ReadAllFileKeys              |   253.18 μs |  15.905 μs |  10.520 μs |        - |        - |       - |         - |
| OverflowScenario             | 2,049.97 μs | 240.701 μs | 159.209 μs |  27.3438 |  27.3438 | 27.3438 |  334442 B |
