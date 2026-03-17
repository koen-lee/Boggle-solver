```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 8540U w/ Radeon 740M Graphics 3.20GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v4


```
| Method                   | Mean     | Error     | StdDev    | Gen0    | Allocated |
|------------------------- |---------:|----------:|----------:|--------:|----------:|
| GetPi_Size63_Threshold16 | 3.720 ms | 0.0393 ms | 0.0307 ms | 70.3125 | 603.75 KB |
| GetPi_Size63_Threshold32 | 3.367 ms | 0.0659 ms | 0.1171 ms | 70.3125 | 603.75 KB |
| GetPi_Size63_Schoolbook  | 4.570 ms | 0.0775 ms | 0.0687 ms | 70.3125 | 582.43 KB |
