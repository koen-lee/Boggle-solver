```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-MJAEKD : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=20  WarmupCount=3  

```
| Method          | Mean      | Error    | StdDev   |
|---------------- |----------:|---------:|---------:|
| Scalar_64bits   |  62.77 μs | 0.650 μs | 0.749 μs |
| Simd_64bits     |  38.26 μs | 0.426 μs | 0.456 μs |
| Scalar_128bits  |  75.53 μs | 0.865 μs | 0.962 μs |
| Simd_128bits    |  54.85 μs | 0.671 μs | 0.772 μs |
| Scalar_192bits  |  89.40 μs | 0.556 μs | 0.618 μs |
| Simd_192bits    |  71.76 μs | 0.658 μs | 0.757 μs |
| Scalar_256bits  | 107.17 μs | 3.040 μs | 3.501 μs |
| Simd_256bits    |  65.39 μs | 1.100 μs | 1.222 μs |
| Scalar_384bits  | 143.04 μs | 6.809 μs | 7.568 μs |
| Simd_384bits    |  69.93 μs | 1.202 μs | 1.384 μs |
| Scalar_512bits  | 164.66 μs | 4.727 μs | 5.254 μs |
| Simd_512bits    |  71.01 μs | 0.460 μs | 0.511 μs |
| Scalar_1024bits | 269.54 μs | 3.314 μs | 3.817 μs |
| Simd_1024bits   |  94.37 μs | 0.742 μs | 0.794 μs |
| Scalar_2048bits | 487.75 μs | 7.596 μs | 8.748 μs |
| Simd_2048bits   | 173.25 μs | 3.831 μs | 4.099 μs |
