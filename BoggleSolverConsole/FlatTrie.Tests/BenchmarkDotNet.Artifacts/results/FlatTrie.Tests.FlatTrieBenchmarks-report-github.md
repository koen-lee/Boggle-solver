```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-MUYHDC : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=10  WarmupCount=3  

```
| Method                       | Mean        | Error      | StdDev     | Gen0    | Gen1    | Gen2    | Allocated |
|----------------------------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
| RandomOrderWrites            | 7,957.39 μs | 341.805 μs | 178.771 μs |       - |       - |       - |   32822 B |
| RandomOrderWrites_Dictionary |    60.31 μs |   1.981 μs |   1.310 μs | 30.2734 | 30.2734 | 30.2734 |  215650 B |
| SequentialWrites             | 3,847.94 μs |  63.258 μs |  41.842 μs |       - |       - |       - |   32819 B |
| GuidWrites                   | 2,872.56 μs |  63.298 μs |  41.868 μs |  3.9063 |       - |       - |   32818 B |
| FileWrites                   | 1,323.95 μs |  19.235 μs |  11.446 μs |  3.9063 |       - |       - |   32817 B |
| ReadAllKeys                  |   400.12 μs |   8.460 μs |   5.596 μs |       - |       - |       - |         - |
| ReadAllKeys_Dictionary       |    23.33 μs |   0.661 μs |   0.437 μs |       - |       - |       - |         - |
| ReadAllGuidKeys              |   146.13 μs |   1.070 μs |   0.636 μs |       - |       - |       - |         - |
| ReadAllFileKeys              |   343.53 μs |   6.560 μs |   4.339 μs |       - |       - |       - |         - |
| OverflowScenario             | 4,446.87 μs |  85.467 μs |  56.531 μs | 23.4375 | 23.4375 | 23.4375 |  334427 B |
