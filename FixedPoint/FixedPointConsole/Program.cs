
﻿namespace FixedPointConsole;
using FixedPoint;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var pi = Fixed<Size8191>.GetPi();
        timer.Stop();
        Console.WriteLine($"Approximated π in {timer.Elapsed.TotalSeconds} seconds.");
        Console.WriteLine($"Pi check: 3.1415926535897932384626433832795028841971");
        Console.WriteLine($"Pi:       {pi.ToDecimalString()}");
        // precomputed value of π to 40 decimal places: 
        
        
    }
}