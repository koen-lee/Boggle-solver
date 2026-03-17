namespace FixedPointConsole;
using FixedPoint;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Console.WriteLine($"Pi: {Fixed.GetPi().ToDecimalString()}");
        Console.WriteLine($"Pi: {Math.PI}");
        // precomputed value of π to 40 decimal places: 
        Console.WriteLine($"Pi: 3.1415926535897932384626433832795028841971");
        
    }
}
