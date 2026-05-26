using System.Runtime.CompilerServices;
using StdMath = OS.Std.Math;
using OS.Std;

internal static class StdMathTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunMinMaxIntegerHelpers();
        RunClampIntegerBounds();
        RunAbsOverflowBehavior();
    }

    private static void RunMinMaxIntegerHelpers()
    {
        const string name = "std.math min max integer helpers";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ExpectBool("status", true, StdMath.Status == CompatibilityStatus.Partial);

            ExpectInt("Min int negative", -7, StdMath.Min(-7, 3));
            ExpectInt("Min int equal", 4, StdMath.Min(4, 4));
            ExpectInt("Max int positive", 9, StdMath.Max(-2, 9));
            ExpectInt("Max int equal", 4, StdMath.Max(4, 4));

            ExpectUInt("Min uint", 3, StdMath.Min(9u, 3u));
            ExpectUInt("Max uint", 9, StdMath.Max(9u, 3u));
            ExpectLong("Min long", long.MinValue + 1, StdMath.Min(long.MinValue + 1, 0));
            ExpectLong("Max long", long.MaxValue, StdMath.Max(0, long.MaxValue));
            ExpectULong("Min ulong", 5, StdMath.Min(5UL, ulong.MaxValue));
            ExpectULong("Max ulong", ulong.MaxValue, StdMath.Max(5UL, ulong.MaxValue));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunClampIntegerBounds()
    {
        const string name = "std.math clamp integer bounds";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ExpectInt("Clamp int below", -2, StdMath.Clamp(-9, -2, 7));
            ExpectInt("Clamp int inside", 3, StdMath.Clamp(3, -2, 7));
            ExpectInt("Clamp int above", 7, StdMath.Clamp(99, -2, 7));

            ExpectUInt("Clamp uint below", 2, StdMath.Clamp(1u, 2u, 7u));
            ExpectUInt("Clamp uint inside", 3, StdMath.Clamp(3u, 2u, 7u));
            ExpectUInt("Clamp uint above", 7, StdMath.Clamp(99u, 2u, 7u));

            ExpectLong("Clamp long below", -2, StdMath.Clamp(long.MinValue, -2, 7));
            ExpectLong("Clamp long inside", 3, StdMath.Clamp(3, -2, 7));
            ExpectLong("Clamp long above", 7, StdMath.Clamp(long.MaxValue, -2, 7));

            ExpectULong("Clamp ulong below", 2, StdMath.Clamp(1UL, 2UL, 7UL));
            ExpectULong("Clamp ulong inside", 3, StdMath.Clamp(3UL, 2UL, 7UL));
            ExpectULong("Clamp ulong above", 7, StdMath.Clamp(99UL, 2UL, 7UL));

            ExpectArgumentException("Clamp int invalid bounds", () => StdMath.Clamp(0, 2, 1));
            ExpectArgumentException("Clamp uint invalid bounds", () => StdMath.Clamp(0u, 2u, 1u));
            ExpectArgumentException("Clamp long invalid bounds", () => StdMath.Clamp(0L, 2L, 1L));
            ExpectArgumentException("Clamp ulong invalid bounds", () => StdMath.Clamp(0UL, 2UL, 1UL));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunAbsOverflowBehavior()
    {
        const string name = "std.math abs overflow behavior";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ExpectInt("Abs int positive", 7, StdMath.Abs(7));
            ExpectInt("Abs int zero", 0, StdMath.Abs(0));
            ExpectInt("Abs int negative", 7, StdMath.Abs(-7));
            ExpectLong("Abs long positive", 7, StdMath.Abs(7L));
            ExpectLong("Abs long zero", 0, StdMath.Abs(0L));
            ExpectLong("Abs long negative", 7, StdMath.Abs(-7L));

            ExpectOverflowException("Abs int min", () => StdMath.Abs(int.MinValue));
            ExpectOverflowException("Abs long min", () => StdMath.Abs(long.MinValue));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void ExpectInt(string scenario, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectUInt(string scenario, uint expected, uint actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectLong(string scenario, long expected, long actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectULong(string scenario, ulong expected, ulong actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectArgumentException(string scenario, Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                scenario + " expected ArgumentException but threw " + ex.GetType().Name + ".");
        }

        throw new InvalidOperationException(scenario + " expected ArgumentException but completed successfully.");
    }

    private static void ExpectOverflowException(string scenario, Action action)
    {
        try
        {
            action();
        }
        catch (OverflowException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                scenario + " expected OverflowException but threw " + ex.GetType().Name + ".");
        }

        throw new InvalidOperationException(scenario + " expected OverflowException but completed successfully.");
    }
}
