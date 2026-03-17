namespace FixedPointConsole;

using FixedPoint;

class Program
{
    static void Main(string[] args)
    {
        Karatsuba.OnMismatch32 = (a, b, rK, rS) =>
        {
            static string Hex(uint[] w) => string.Concat(w.Reverse().Select(x => x.ToString("X8")));
            Console.WriteLine($"=== First n=32 mismatch ===");
            Console.WriteLine($"A        : {Hex(a)}");
            Console.WriteLine($"B        : {Hex(b)}");
            Console.WriteLine($"Karatsuba: {Hex(rK)}");
            Console.WriteLine($"Schoolbk : {Hex(rS)}");
        };
        new Fixed<Size31>(2).Sqrt();
        if (Karatsuba.OnMismatch32 != null) Console.WriteLine("No mismatch found in Sqrt<Size31>");
    }

    static void DumpMismatch31()
    {
        var operandHex = new Fixed<Size31Schoolbook>(2).Sqrt().ToHexString();
        var a  = Fixed<Size31>          .ParseHexStringExact(operandHex);
        var rK = (a * a).ToHexString();
        var rS = (Fixed<Size31Schoolbook>.ParseHexStringExact(operandHex) *
                  Fixed<Size31Schoolbook>.ParseHexStringExact(operandHex)).ToHexString();
        int first = Enumerable.Range(0, rK.Length).FirstOrDefault(i => rK[i] != rS[i], -1);
        Console.WriteLine($"Operand : {operandHex}");
        Console.WriteLine($"Karatsuba: {rK}");
        Console.WriteLine($"Schoolbk : {rS}");
        Console.WriteLine(first < 0 ? "Results identical" : $"First diff at index {first}");
    }

    static void CompareSqrt2<TK, TS>(string label)
        where TK : struct, IFixedSize
        where TS : struct, IFixedSize
    {
        var sk = new Fixed<TK>(2).Sqrt().ToHexString();
        var sb = new Fixed<TS>(2).Sqrt().ToHexString();
        var first = Enumerable.Range(0, sk.Length).FirstOrDefault(i => sk[i] != sb[i], -1);
        if (first < 0)
            Console.WriteLine($"{label}: identical");
        else
        {
            int word = (first - 2) / 8; // fraction word from MSB (0-indexed)
            Console.WriteLine($"{label}: first diff at hex index {first} (fraction word {word} from MSB)");
            Console.WriteLine($"  karatsuba : ...{sk[Math.Max(0,first-8)..(first+8)]}...");
            Console.WriteLine($"  schoolbook: ...{sb[Math.Max(0,first-8)..(first+8)]}...");
        }
    }
}
