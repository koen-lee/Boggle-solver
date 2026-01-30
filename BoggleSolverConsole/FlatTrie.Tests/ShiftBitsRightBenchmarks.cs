using BenchmarkDotNet.Attributes;
using BitUtilities;

namespace FlatTrie.Tests;

[SimpleJob(warmupCount: 3, iterationCount: 20)]
public class ShiftBitsRightBenchmarks
{
    private uint[] _buffer = null!;
    private const int BufferSize = 128; // 4KB buffer
    private const int BatchSize = 10_000;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new uint[BufferSize];
        // Fill with recognizable pattern
        for (int i = 0; i < BufferSize; i++)
            _buffer[i] = (uint)(0xDEAD0000 | i);
    }

    // Test various bitsToMove sizes with non-aligned delta (most common case)
    // Delta of 47 bits ensures we hit the general barrel-shift path (rot != 0)
    // Each benchmark runs BatchSize iterations internally for stable measurements

    [Benchmark]
    public uint Scalar_64bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 64, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_64bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 64, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_128bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 128, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_128bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 128, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_192bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 192, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_192bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 192, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_256bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 256, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_256bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 256, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_384bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 384, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_384bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 384, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_512bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 512, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_512bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 512, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_1024bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 1024, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_1024bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 1024, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Scalar_2048bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRight(_buffer, 50, 2048, 47);
        return _buffer[0];
    }

    [Benchmark]
    public uint Simd_2048bits()
    {
        for (int i = 0; i < BatchSize; i++)
            BitArrayWriter.ShiftBitsRightSimd(_buffer, 50, 2048, 47);
        return _buffer[0];
    }
}
