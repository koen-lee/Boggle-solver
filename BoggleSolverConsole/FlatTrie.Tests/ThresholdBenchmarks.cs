using BenchmarkDotNet.Attributes;
using BitUtilities;

namespace FlatTrie.Tests;

/// <summary>
/// Benchmarks to determine optimal threshold for SIMD path.
/// Compares ShiftBitsRightSimd (threshold >= 7) vs _nothreshold variant.
/// Tests word spans 3-6 where threshold matters.
/// </summary>
[SimpleJob(warmupCount: 3, iterationCount: 20)]
public class ThresholdBenchmarks
{
    private uint[] _buffer = null!;
    private const int BufferSize = 128;
    private const int BatchSize = 10_000;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new uint[BufferSize];
        for (int i = 0; i < BufferSize; i++)
            _buffer[i] = (uint)(0xDEAD0000 | i);
    }

    // 3 words (96 bits) - below threshold, SIMD loop won't run
    [Benchmark]
    public uint Threshold_3words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 96, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_3words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 96, 47);
        return _buffer[0];
    }

    // 4 words (128 bits) - below threshold
    [Benchmark]
    public uint Threshold_4words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 128, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_4words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 128, 47);
        return _buffer[0];
    }

    // 5 words (160 bits) - below threshold
    [Benchmark]
    public uint Threshold_5words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 160, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_5words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 160, 47);
        return _buffer[0];
    }

    // 6 words (192 bits) - below threshold
    [Benchmark]
    public uint Threshold_6words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 192, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_6words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 192, 47);
        return _buffer[0];
    }

    // 7 words (224 bits) - at threshold boundary
    [Benchmark]
    public uint Threshold_7words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 224, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_7words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 224, 47);
        return _buffer[0];
    }

    // 8 words (256 bits) - just above threshold
    [Benchmark]
    public uint Threshold_8words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 256, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint NoThreshold_8words()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd_nothreshold(_buffer, 50, 256, 47);
        return _buffer[0];
    }
}
