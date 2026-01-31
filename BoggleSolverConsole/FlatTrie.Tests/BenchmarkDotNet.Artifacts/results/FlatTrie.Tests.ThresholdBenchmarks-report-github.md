```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-XOAPSZ : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=20  WarmupCount=3  

```
| Method             | Mean      | Error     | StdDev    |
|------------------- |----------:|----------:|----------:|
| Threshold_3words   |  39.37 μs |  0.254 μs |  0.282 μs |
| NoThreshold_3words |  89.12 μs | 15.301 μs | 17.621 μs |
| Threshold_4words   |  91.76 μs |  5.721 μs |  6.589 μs |
| NoThreshold_4words |  89.20 μs |  1.407 μs |  1.620 μs |
| Threshold_5words   | 102.81 μs |  8.730 μs | 10.054 μs |
| NoThreshold_5words |  95.40 μs |  0.932 μs |  1.073 μs |
| Threshold_6words   | 118.92 μs |  7.365 μs |  8.481 μs |
| NoThreshold_6words |  94.46 μs |  1.405 μs |  1.618 μs |
| Threshold_7words   | 105.45 μs |  0.738 μs |  0.850 μs |
| NoThreshold_7words | 105.32 μs |  0.538 μs |  0.619 μs |
| Threshold_8words   |  92.06 μs |  0.824 μs |  0.949 μs |
| NoThreshold_8words |  93.00 μs |  1.636 μs |  1.680 μs |
