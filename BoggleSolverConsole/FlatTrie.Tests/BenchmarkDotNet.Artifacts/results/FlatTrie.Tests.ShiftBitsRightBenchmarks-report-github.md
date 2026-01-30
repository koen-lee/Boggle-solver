```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7623)
Unknown processor
.NET SDK 10.0.102
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-KAKBFV : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=20  WarmupCount=3  

```
| Method          | Mean      | Error     | StdDev    |
|---------------- |----------:|----------:|----------:|
| Scalar_64bits   |  63.09 μs |  0.880 μs |  0.978 μs |
| Simd_64bits     |  38.00 μs |  0.630 μs |  0.700 μs |
| Scalar_128bits  |  74.69 μs |  0.553 μs |  0.592 μs |
| Simd_128bits    |  55.20 μs |  0.805 μs |  0.895 μs |
| Scalar_192bits  |  91.95 μs |  3.185 μs |  3.408 μs |
| Simd_192bits    |  75.95 μs |  2.191 μs |  2.435 μs |
| Scalar_256bits  | 104.37 μs |  2.600 μs |  2.782 μs |
| Simd_256bits    |  65.04 μs |  1.309 μs |  1.400 μs |
| Scalar_384bits  | 130.32 μs |  1.855 μs |  2.062 μs |
| Simd_384bits    |  67.97 μs |  0.681 μs |  0.785 μs |
| Scalar_512bits  | 158.15 μs |  1.723 μs |  1.915 μs |
| Simd_512bits    |  74.39 μs |  2.054 μs |  2.198 μs |
| Scalar_1024bits | 272.26 μs |  6.371 μs |  7.337 μs |
| Simd_1024bits   |  98.36 μs |  2.954 μs |  3.283 μs |
| Scalar_2048bits | 509.52 μs | 10.163 μs | 11.704 μs |
| Simd_2048bits   | 182.40 μs |  5.129 μs |  5.488 μs |
