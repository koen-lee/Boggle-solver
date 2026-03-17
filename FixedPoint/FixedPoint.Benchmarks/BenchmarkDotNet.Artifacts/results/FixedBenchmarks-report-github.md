```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8037)
Unknown processor
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method     | Mean         | Error       | StdDev      | Median       | Gen0   | Allocated |
|----------- |-------------:|------------:|------------:|-------------:|-------:|----------:|
| Add        |     4.172 ns |   0.0599 ns |   0.0561 ns |     4.171 ns | 0.0057 |      48 B |
| Subtract   |     4.138 ns |   0.0331 ns |   0.0276 ns |     4.137 ns | 0.0057 |      48 B |
| Multiply   |    34.824 ns |   0.3177 ns |   0.2653 ns |    34.836 ns | 0.0057 |      48 B |
| Divide     |   263.093 ns |   5.0344 ns |   4.7092 ns |   261.911 ns | 0.0687 |     576 B |
| Reciprocal |   228.657 ns |   0.9995 ns |   0.8346 ns |   228.875 ns | 0.0629 |     528 B |
| Sqrt       |   889.572 ns |  16.1918 ns |  27.9300 ns |   882.970 ns | 0.2518 |    2112 B |
| GetPi      | 6,888.867 ns | 117.9819 ns | 104.5879 ns | 6,840.438 ns | 2.1210 |   17760 B |
| ToDecimal  |   120.169 ns |   2.4447 ns |   4.7101 ns |   118.153 ns | 0.0629 |     528 B |
