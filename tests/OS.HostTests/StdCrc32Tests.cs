using System.Runtime.CompilerServices;
using OS.Std.Hash;

internal static class StdCrc32Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunScenario("std.crc32 empty input", TestEmptyInput);
        RunScenario("std.crc32 ascii check vector", TestAsciiCheckVector);
        RunScenario("std.crc32 binary vector", TestBinaryVector);
        RunScenario("std.crc32 incremental update", TestIncrementalUpdate);
    }

    private static void TestEmptyInput()
    {
        ExpectValue("empty", 0x00000000u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
    }

    private static void TestAsciiCheckVector()
    {
        ExpectValue("123456789", 0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    private static void TestBinaryVector()
    {
        ReadOnlySpan<byte> value = new byte[]
        {
            0x00, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08, 0x09
        };

        ExpectValue("range 0..9", 0x456CD746u, Crc32.Compute(value));
    }

    private static void TestIncrementalUpdate()
    {
        ReadOnlySpan<byte> first = new byte[] { 0xDE, 0xAD };
        ReadOnlySpan<byte> second = new byte[] { 0xBE, 0xEF };

        uint state = Crc32.Update(Crc32.InitialState, first);
        state = Crc32.Update(state, second);

        ExpectValue("split deadbeef", 0x7C9CA35Au, Crc32.Finish(state));
    }

    private static void RunScenario(string name, Action test)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            test();
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void ExpectValue(string scenario, uint expected, uint actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected 0x" + expected.ToString("X8") + " but returned 0x" + actual.ToString("X8") + ".");
        }
    }
}
