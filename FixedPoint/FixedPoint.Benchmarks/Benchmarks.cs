using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FixedPoint;

BenchmarkRunner.Run<FixedBenchmarks>();

[MemoryDiagnoser]
public class FixedBenchmarks
{
    static readonly Fixed A = new Fixed(2).Sqrt();   // √2
    static readonly Fixed B = Fixed.GetPi();          // π
    static readonly Fixed Two = new Fixed(2);

    [Benchmark] public Fixed Add()        => A + B;
    [Benchmark] public Fixed Subtract()   => A - B;
    [Benchmark] public Fixed Multiply()   => A * B;
    [Benchmark] public Fixed Divide()     => A / B;
    [Benchmark] public Fixed Reciprocal() => A.Reciprocal();
    [Benchmark] public Fixed Sqrt()       => Two.Sqrt();
    [Benchmark] public Fixed GetPi()      => Fixed.GetPi();
    [Benchmark] public string ToDecimal() => B.ToDecimalString();
}
