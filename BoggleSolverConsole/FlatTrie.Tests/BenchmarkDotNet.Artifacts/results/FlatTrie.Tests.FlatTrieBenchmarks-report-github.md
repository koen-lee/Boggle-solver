```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-ETBTVN : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0     | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|---------:|--------:|--------:|----------:|
| RandomOrderWrites            | 9,063.84 μs | 423.804 μs | 221.658 μs |  78.1250 |       - |       - |  734022 B |
| RandomOrderWrites_Dictionary |    61.10 μs |   4.282 μs |   2.832 μs |  30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 3,305.07 μs |  30.086 μs |  19.900 μs | 101.5625 |  7.8125 |       - |  866746 B |
| GuidWrites                   | 2,532.80 μs |  30.723 μs |  20.321 μs | 105.4688 | 11.7188 |       - |  902258 B |
| FileWrites                   | 1,651.86 μs |  70.111 μs |  41.722 μs | 115.2344 | 11.7188 |       - |  966769 B |
| ReadAllKeys                  |   443.13 μs |  27.860 μs |  16.579 μs |  16.1133 |       - |       - |  137472 B |
| ReadAllKeys_Dictionary       |    22.99 μs |   1.040 μs |   0.619 μs |        - |       - |       - |         - |
| ReadAllGuidKeys              |   169.11 μs |   3.521 μs |   2.096 μs |   9.7656 |       - |       - |   82208 B |
| ReadAllFileKeys              |   393.06 μs |  11.012 μs |   7.284 μs |  27.8320 |       - |       - |  233120 B |
| OverflowScenario             | 3,879.37 μs |  78.612 μs |  51.997 μs | 117.1875 | 39.0625 | 23.4375 | 1077532 B |
