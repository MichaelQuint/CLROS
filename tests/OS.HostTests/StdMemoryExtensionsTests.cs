using System.Runtime.CompilerServices;
using StdMemoryExtensions = OS.Std.MemoryExtensions;
using OS.Std;

internal static class StdMemoryExtensionsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunStartsWithEndsWithContract();
        RunCountByteValuesContract();
        RunIndexOfAnyByteValuesContract();
        RunTryCopyToSuccessContract();
        RunTryCopyToTooSmallContract();
        RunTryCopyToOverlapContract();
        RunCharSearchContract();
        RunCharSequencePrefixSuffixContract();
        RunCharCopyToContract();
        RunCharTryCopyToContract();

        const string name = "std.memoryextensions byte span helpers";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] empty = Array.Empty<byte>();
            byte[] source = new byte[] { 1, 2, 3, 2, 4 };
            byte[] same = new byte[] { 1, 2, 3, 2, 4 };
            byte[] differentTail = new byte[] { 1, 2, 3, 2, 5 };
            byte[] shorter = new byte[] { 1, 2, 3, 2 };
            byte[] repeatedTail = new byte[] { 1, 2, 3, 2, 4, 2 };

            ExpectBool("Status", true, StdMemoryExtensions.Status == CompatibilityStatus.Partial);

            ExpectBool("SequenceEqual empty", true, StdMemoryExtensions.SequenceEqual(empty, Array.Empty<byte>()));
            ExpectBool("SequenceEqual same", true, StdMemoryExtensions.SequenceEqual(source, same));
            ExpectBool("SequenceEqual different tail", false, StdMemoryExtensions.SequenceEqual(source, differentTail));
            ExpectBool("SequenceEqual shorter", false, StdMemoryExtensions.SequenceEqual(source, shorter));

            ExpectBool("StartsWith empty", true, StdMemoryExtensions.StartsWith(source, Array.Empty<byte>()));
            ExpectBool("StartsWith prefix", true, StdMemoryExtensions.StartsWith(source, new byte[] { 1, 2 }));
            ExpectBool("StartsWith exact", true, StdMemoryExtensions.StartsWith(source, same));
            ExpectBool("StartsWith mismatch", false, StdMemoryExtensions.StartsWith(source, new byte[] { 2, 3 }));
            ExpectBool("StartsWith longer", false, StdMemoryExtensions.StartsWith(shorter, source));

            ExpectBool("EndsWith empty", true, StdMemoryExtensions.EndsWith(source, Array.Empty<byte>()));
            ExpectBool("EndsWith suffix", true, StdMemoryExtensions.EndsWith(source, new byte[] { 2, 4 }));
            ExpectBool("EndsWith exact", true, StdMemoryExtensions.EndsWith(source, same));
            ExpectBool("EndsWith mismatch", false, StdMemoryExtensions.EndsWith(source, new byte[] { 3, 4 }));
            ExpectBool("EndsWith longer", false, StdMemoryExtensions.EndsWith(shorter, source));

            ExpectInt("IndexOf byte first", 0, StdMemoryExtensions.IndexOf(source, (byte)1));
            ExpectInt("IndexOf byte repeated", 1, StdMemoryExtensions.IndexOf(source, (byte)2));
            ExpectInt("IndexOf byte missing", -1, StdMemoryExtensions.IndexOf(source, (byte)9));
            ExpectInt("IndexOf byte empty", -1, StdMemoryExtensions.IndexOf(empty, (byte)1));

            ExpectInt("Count byte repeated", 2, StdMemoryExtensions.Count(source, (byte)2));
            ExpectInt("Count byte absent", 0, StdMemoryExtensions.Count(source, (byte)9));
            ExpectInt("Count byte empty", 0, StdMemoryExtensions.Count(empty, (byte)1));

            ExpectInt("IndexOfAny two values first", 1, StdMemoryExtensions.IndexOfAny(source, (byte)9, (byte)2));
            ExpectInt("IndexOfAny two values second", 2, StdMemoryExtensions.IndexOfAny(source, (byte)3, (byte)9));
            ExpectInt("IndexOfAny two values missing", -1, StdMemoryExtensions.IndexOfAny(source, (byte)8, (byte)9));
            ExpectInt("IndexOfAny three values first", 1, StdMemoryExtensions.IndexOfAny(source, (byte)9, (byte)2, (byte)4));
            ExpectInt("IndexOfAny three values second", 2, StdMemoryExtensions.IndexOfAny(source, (byte)9, (byte)8, (byte)3));
            ExpectInt("IndexOfAny three values missing", -1, StdMemoryExtensions.IndexOfAny(source, (byte)7, (byte)8, (byte)9));

            ExpectInt("LastIndexOf byte empty", -1, StdMemoryExtensions.LastIndexOf(empty, (byte)1));
            ExpectInt("LastIndexOf byte missing", -1, StdMemoryExtensions.LastIndexOf(source, (byte)9));
            ExpectInt("LastIndexOf byte first", 0, StdMemoryExtensions.LastIndexOf(source, (byte)1));
            ExpectInt("LastIndexOf byte middle", 3, StdMemoryExtensions.LastIndexOf(source, (byte)2));
            ExpectInt("LastIndexOf byte repeated tail", 5, StdMemoryExtensions.LastIndexOf(repeatedTail, (byte)2));

            ExpectInt("IndexOf span empty needle", 0, StdMemoryExtensions.IndexOf(source, Array.Empty<byte>()));
            ExpectInt("IndexOf span prefix", 0, StdMemoryExtensions.IndexOf(source, new byte[] { 1, 2, 3 }));
            ExpectInt("IndexOf span middle", 2, StdMemoryExtensions.IndexOf(source, new byte[] { 3, 2 }));
            ExpectInt("IndexOf span suffix", 3, StdMemoryExtensions.IndexOf(source, new byte[] { 2, 4 }));
            ExpectInt("IndexOf span missing", -1, StdMemoryExtensions.IndexOf(source, new byte[] { 3, 4 }));
            ExpectInt("IndexOf span longer", -1, StdMemoryExtensions.IndexOf(shorter, source));
            ExpectInt(
                "IndexOf span repeated pattern",
                1,
                StdMemoryExtensions.IndexOf(new byte[] { 9, 1, 9, 1, 9 }, new byte[] { 1, 9, 1 }));

            ExpectInt("LastIndexOf span empty needle", source.Length, StdMemoryExtensions.LastIndexOf(source, Array.Empty<byte>()));
            ExpectInt("LastIndexOf span exact", 0, StdMemoryExtensions.LastIndexOf(source, same));
            ExpectInt("LastIndexOf span middle", 2, StdMemoryExtensions.LastIndexOf(source, new byte[] { 3, 2 }));
            ExpectInt(
                "LastIndexOf span repeated pattern",
                2,
                StdMemoryExtensions.LastIndexOf(new byte[] { 1, 2, 1, 2, 1 }, new byte[] { 1, 2, 1 }));
            ExpectInt("LastIndexOf span missing", -1, StdMemoryExtensions.LastIndexOf(source, new byte[] { 3, 4 }));
            ExpectInt("LastIndexOf span longer", -1, StdMemoryExtensions.LastIndexOf(shorter, source));

            ExpectBool("Contains present", true, StdMemoryExtensions.Contains(source, (byte)4));
            ExpectBool("Contains absent", false, StdMemoryExtensions.Contains(source, (byte)8));
            ExpectBool("Contains empty", false, StdMemoryExtensions.Contains(empty, (byte)1));

            byte[] copyExact = new byte[source.Length];
            StdMemoryExtensions.CopyTo(source, copyExact);
            ExpectBool("CopyTo exact", true, StdMemoryExtensions.SequenceEqual(copyExact, source));

            byte[] copyLarger = new byte[] { 9, 9, 9, 9, 9, 9, 9 };
            StdMemoryExtensions.CopyTo(source, copyLarger);
            ExpectBool("CopyTo larger prefix", true, StdMemoryExtensions.StartsWith(copyLarger, source));
            ExpectInt("CopyTo larger tail first", 9, copyLarger[source.Length]);
            ExpectInt("CopyTo larger tail second", 9, copyLarger[source.Length + 1]);

            byte[] copyOverlapForward = new byte[] { 1, 2, 3, 4, 5 };
            StdMemoryExtensions.CopyTo(copyOverlapForward.AsSpan(1, 4), copyOverlapForward.AsSpan(0, 4));
            ExpectBool(
                "CopyTo overlap forward",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapForward, new byte[] { 2, 3, 4, 5, 5 }));

            byte[] copyOverlapBackward = new byte[] { 1, 2, 3, 4, 5 };
            StdMemoryExtensions.CopyTo(copyOverlapBackward.AsSpan(0, 4), copyOverlapBackward.AsSpan(1, 4));
            ExpectBool(
                "CopyTo overlap backward",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapBackward, new byte[] { 1, 1, 2, 3, 4 }));

            byte[] tooSmallDestination = new byte[] { 9, 9 };
            ExpectArgumentException(
                "CopyTo too-small destination",
                () => StdMemoryExtensions.CopyTo(source, tooSmallDestination));
            ExpectBool(
                "CopyTo too-small preserves destination",
                true,
                StdMemoryExtensions.SequenceEqual(tooSmallDestination, new byte[] { 9, 9 }));

            byte[] tryCopyExact = new byte[source.Length];
            ExpectBool("TryCopyTo exact result", true, StdMemoryExtensions.TryCopyTo(source, tryCopyExact));
            ExpectBool("TryCopyTo exact destination", true, StdMemoryExtensions.SequenceEqual(tryCopyExact, source));

            byte[] tryCopyTooSmallDestination = new byte[] { 9, 9 };
            ExpectBool("TryCopyTo too-small result", false, StdMemoryExtensions.TryCopyTo(source, tryCopyTooSmallDestination));
            ExpectBool(
                "TryCopyTo too-small preserves destination",
                true,
                StdMemoryExtensions.SequenceEqual(tryCopyTooSmallDestination, new byte[] { 9, 9 }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunCountByteValuesContract()
    {
        const string name = "std.memoryextensions count byte values";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] empty = Array.Empty<byte>();
            byte[] source = new byte[] { 0, 1, 2, 1, 3, 1, 0 };
            byte[] allSame = new byte[] { 7, 7, 7, 7 };

            ExpectInt("Count empty", 0, StdMemoryExtensions.Count(empty, (byte)1));
            ExpectInt("Count absent", 0, StdMemoryExtensions.Count(source, (byte)9));
            ExpectInt("Count repeated", 3, StdMemoryExtensions.Count(source, (byte)1));
            ExpectInt("Count zero byte", 2, StdMemoryExtensions.Count(source, (byte)0));
            ExpectInt("Count all values", allSame.Length, StdMemoryExtensions.Count(allSame, (byte)7));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunIndexOfAnyByteValuesContract()
    {
        const string name = "std.memoryextensions indexofany byte values";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] empty = Array.Empty<byte>();
            byte[] source = new byte[] { 9, 8, 7, 6, 8, 5 };

            ExpectInt("IndexOfAny empty two values", -1, StdMemoryExtensions.IndexOfAny(empty, (byte)1, (byte)2));
            ExpectInt("IndexOfAny two values earliest span index", 1, StdMemoryExtensions.IndexOfAny(source, (byte)6, (byte)8));
            ExpectInt("IndexOfAny two values first argument later", 2, StdMemoryExtensions.IndexOfAny(source, (byte)7, (byte)5));
            ExpectInt("IndexOfAny two values duplicate needles", 1, StdMemoryExtensions.IndexOfAny(source, (byte)8, (byte)8));
            ExpectInt("IndexOfAny two values missing", -1, StdMemoryExtensions.IndexOfAny(source, (byte)1, (byte)2));

            ExpectInt("IndexOfAny empty three values", -1, StdMemoryExtensions.IndexOfAny(empty, (byte)1, (byte)2, (byte)3));
            ExpectInt("IndexOfAny three values earliest span index", 2, StdMemoryExtensions.IndexOfAny(source, (byte)5, (byte)6, (byte)7));
            ExpectInt("IndexOfAny three values first argument later", 3, StdMemoryExtensions.IndexOfAny(source, (byte)6, (byte)4, (byte)3));
            ExpectInt("IndexOfAny three values duplicate needles", 1, StdMemoryExtensions.IndexOfAny(source, (byte)8, (byte)8, (byte)4));
            ExpectInt("IndexOfAny three values missing", -1, StdMemoryExtensions.IndexOfAny(source, (byte)1, (byte)2, (byte)3));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryCopyToSuccessContract()
    {
        const string name = "std.memoryextensions trycopyto success";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] source = new byte[] { 1, 2, 3 };
            byte[] destination = new byte[] { 9, 9, 9, 9 };

            ExpectBool("TryCopyTo returns true", true, StdMemoryExtensions.TryCopyTo(source, destination));
            ExpectBool(
                "TryCopyTo writes prefix",
                true,
                StdMemoryExtensions.SequenceEqual(destination, new byte[] { 1, 2, 3, 9 }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryCopyToTooSmallContract()
    {
        const string name = "std.memoryextensions trycopyto too-small";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] source = new byte[] { 1, 2, 3 };
            byte[] destination = new byte[] { 9, 8 };

            ExpectBool("TryCopyTo returns false", false, StdMemoryExtensions.TryCopyTo(source, destination));
            ExpectBool(
                "TryCopyTo leaves destination unchanged",
                true,
                StdMemoryExtensions.SequenceEqual(destination, new byte[] { 9, 8 }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryCopyToOverlapContract()
    {
        const string name = "std.memoryextensions trycopyto overlap";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] copyOverlapForward = new byte[] { 1, 2, 3, 4, 5 };
            ExpectBool(
                "TryCopyTo overlap forward result",
                true,
                StdMemoryExtensions.TryCopyTo(copyOverlapForward.AsSpan(1, 4), copyOverlapForward.AsSpan(0, 4)));
            ExpectBool(
                "TryCopyTo overlap forward destination",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapForward, new byte[] { 2, 3, 4, 5, 5 }));

            byte[] copyOverlapBackward = new byte[] { 1, 2, 3, 4, 5 };
            ExpectBool(
                "TryCopyTo overlap backward result",
                true,
                StdMemoryExtensions.TryCopyTo(copyOverlapBackward.AsSpan(0, 4), copyOverlapBackward.AsSpan(1, 4)));
            ExpectBool(
                "TryCopyTo overlap backward destination",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapBackward, new byte[] { 1, 1, 2, 3, 4 }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunStartsWithEndsWithContract()
    {
        const string name = "std.memoryextensions byte span startswith endswith";
        Console.WriteLine("TEST_START " + name);

        try
        {
            byte[] source = new byte[] { 1, 2, 3, 4 };
            byte[] exact = new byte[] { 1, 2, 3, 4 };
            byte[] empty = Array.Empty<byte>();

            ExpectBool("StartsWith empty prefix", true, StdMemoryExtensions.StartsWith(source, empty));
            ExpectBool("StartsWith exact", true, StdMemoryExtensions.StartsWith(source, exact));
            ExpectBool("StartsWith prefix", true, StdMemoryExtensions.StartsWith(source, new byte[] { 1, 2 }));
            ExpectBool("StartsWith mismatch", false, StdMemoryExtensions.StartsWith(source, new byte[] { 2, 3 }));
            ExpectBool("StartsWith too long", false, StdMemoryExtensions.StartsWith(new byte[] { 1, 2 }, source));

            ExpectBool("EndsWith empty suffix", true, StdMemoryExtensions.EndsWith(source, empty));
            ExpectBool("EndsWith exact", true, StdMemoryExtensions.EndsWith(source, exact));
            ExpectBool("EndsWith suffix", true, StdMemoryExtensions.EndsWith(source, new byte[] { 3, 4 }));
            ExpectBool("EndsWith mismatch", false, StdMemoryExtensions.EndsWith(source, new byte[] { 2, 3 }));
            ExpectBool("EndsWith too long", false, StdMemoryExtensions.EndsWith(new byte[] { 3, 4 }, source));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunCharSearchContract()
    {
        const string name = "std.memoryextensions char search";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ReadOnlySpan<char> empty = ReadOnlySpan<char>.Empty;
            ReadOnlySpan<char> source = "alpha beta alpha".AsSpan();

            ExpectInt("IndexOf first char", 0, StdMemoryExtensions.IndexOf(source, 'a'));
            ExpectInt("IndexOf middle char", 6, StdMemoryExtensions.IndexOf(source, 'b'));
            ExpectInt("IndexOf repeated char", 1, StdMemoryExtensions.IndexOf(source, 'l'));
            ExpectInt("IndexOf missing char", -1, StdMemoryExtensions.IndexOf(source, 'z'));
            ExpectInt("IndexOf empty", -1, StdMemoryExtensions.IndexOf(empty, 'a'));

            ExpectInt("LastIndexOf last char", 15, StdMemoryExtensions.LastIndexOf(source, 'a'));
            ExpectInt("LastIndexOf middle char", 6, StdMemoryExtensions.LastIndexOf(source, 'b'));
            ExpectInt("LastIndexOf repeated char", 13, StdMemoryExtensions.LastIndexOf(source, 'p'));
            ExpectInt("LastIndexOf missing char", -1, StdMemoryExtensions.LastIndexOf(source, 'z'));
            ExpectInt("LastIndexOf empty", -1, StdMemoryExtensions.LastIndexOf(empty, 'a'));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunCharSequencePrefixSuffixContract()
    {
        const string name = "std.memoryextensions char sequence prefix suffix";
        Console.WriteLine("TEST_START " + name);

        try
        {
            char[] empty = Array.Empty<char>();
            char[] source = new char[] { 'c', 'l', 'o', 'u', 'd' };
            char[] same = new char[] { 'c', 'l', 'o', 'u', 'd' };
            char[] differentTail = new char[] { 'c', 'l', 'o', 'u', 't' };
            char[] shorter = new char[] { 'c', 'l', 'o' };

            ExpectBool("SequenceEqual empty", true, StdMemoryExtensions.SequenceEqual(empty, Array.Empty<char>()));
            ExpectBool("SequenceEqual same", true, StdMemoryExtensions.SequenceEqual(source, same));
            ExpectBool("SequenceEqual different tail", false, StdMemoryExtensions.SequenceEqual(source, differentTail));
            ExpectBool("SequenceEqual shorter", false, StdMemoryExtensions.SequenceEqual(source, shorter));

            ExpectBool("StartsWith empty", true, StdMemoryExtensions.StartsWith(source, empty));
            ExpectBool("StartsWith prefix", true, StdMemoryExtensions.StartsWith(source, new char[] { 'c', 'l' }));
            ExpectBool("StartsWith exact", true, StdMemoryExtensions.StartsWith(source, same));
            ExpectBool("StartsWith mismatch", false, StdMemoryExtensions.StartsWith(source, new char[] { 'l', 'o' }));
            ExpectBool("StartsWith longer", false, StdMemoryExtensions.StartsWith(shorter, source));

            ExpectBool("EndsWith empty", true, StdMemoryExtensions.EndsWith(source, empty));
            ExpectBool("EndsWith suffix", true, StdMemoryExtensions.EndsWith(source, new char[] { 'u', 'd' }));
            ExpectBool("EndsWith exact", true, StdMemoryExtensions.EndsWith(source, same));
            ExpectBool("EndsWith mismatch", false, StdMemoryExtensions.EndsWith(source, new char[] { 'o', 'd' }));
            ExpectBool("EndsWith longer", false, StdMemoryExtensions.EndsWith(shorter, source));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunCharCopyToContract()
    {
        const string name = "std.memoryextensions char copyto";
        Console.WriteLine("TEST_START " + name);

        try
        {
            char[] source = new char[] { 'O', 'S', '1' };
            char[] copyExact = new char[source.Length];
            StdMemoryExtensions.CopyTo(source, copyExact);
            ExpectBool("CopyTo exact", true, StdMemoryExtensions.SequenceEqual(copyExact, source));

            char[] copyLarger = new char[] { 'x', 'x', 'x', 'x', 'x' };
            StdMemoryExtensions.CopyTo(source, copyLarger);
            ExpectBool("CopyTo larger prefix", true, StdMemoryExtensions.StartsWith(copyLarger, source));
            ExpectBool("CopyTo larger preserves tail", true, copyLarger[3] == 'x' && copyLarger[4] == 'x');

            char[] copyOverlapForward = new char[] { 'a', 'b', 'c', 'd', 'e' };
            StdMemoryExtensions.CopyTo(copyOverlapForward.AsSpan(1, 4), copyOverlapForward.AsSpan(0, 4));
            ExpectBool(
                "CopyTo overlap forward",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapForward, new char[] { 'b', 'c', 'd', 'e', 'e' }));

            char[] copyOverlapBackward = new char[] { 'a', 'b', 'c', 'd', 'e' };
            StdMemoryExtensions.CopyTo(copyOverlapBackward.AsSpan(0, 4), copyOverlapBackward.AsSpan(1, 4));
            ExpectBool(
                "CopyTo overlap backward",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapBackward, new char[] { 'a', 'a', 'b', 'c', 'd' }));

            char[] emptyDestination = new char[] { 'z' };
            StdMemoryExtensions.CopyTo(Array.Empty<char>(), emptyDestination);
            ExpectBool("CopyTo empty source preserves destination", true, emptyDestination[0] == 'z');

            char[] tooSmallDestination = new char[] { '9', '9' };
            ExpectArgumentException(
                "CopyTo too-small destination",
                () => StdMemoryExtensions.CopyTo(source, tooSmallDestination));
            ExpectBool(
                "CopyTo too-small preserves destination",
                true,
                StdMemoryExtensions.SequenceEqual(tooSmallDestination, new char[] { '9', '9' }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunCharTryCopyToContract()
    {
        const string name = "std.memoryextensions char trycopyto";
        Console.WriteLine("TEST_START " + name);

        try
        {
            char[] source = new char[] { 'C', 'L', 'R' };
            char[] destination = new char[] { 'x', 'x', 'x', 'x' };
            ExpectBool("TryCopyTo returns true", true, StdMemoryExtensions.TryCopyTo(source, destination));
            ExpectBool(
                "TryCopyTo writes prefix",
                true,
                StdMemoryExtensions.SequenceEqual(destination, new char[] { 'C', 'L', 'R', 'x' }));

            char[] tooSmallDestination = new char[] { '9', '8' };
            ExpectBool("TryCopyTo returns false", false, StdMemoryExtensions.TryCopyTo(source, tooSmallDestination));
            ExpectBool(
                "TryCopyTo leaves destination unchanged",
                true,
                StdMemoryExtensions.SequenceEqual(tooSmallDestination, new char[] { '9', '8' }));

            char[] emptyDestination = new char[] { 'z' };
            ExpectBool("TryCopyTo empty returns true", true, StdMemoryExtensions.TryCopyTo(Array.Empty<char>(), emptyDestination));
            ExpectBool("TryCopyTo empty preserves destination", true, emptyDestination[0] == 'z');

            char[] copyOverlapForward = new char[] { 'a', 'b', 'c', 'd', 'e' };
            ExpectBool(
                "TryCopyTo overlap forward result",
                true,
                StdMemoryExtensions.TryCopyTo(copyOverlapForward.AsSpan(1, 4), copyOverlapForward.AsSpan(0, 4)));
            ExpectBool(
                "TryCopyTo overlap forward destination",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapForward, new char[] { 'b', 'c', 'd', 'e', 'e' }));

            char[] copyOverlapBackward = new char[] { 'a', 'b', 'c', 'd', 'e' };
            ExpectBool(
                "TryCopyTo overlap backward result",
                true,
                StdMemoryExtensions.TryCopyTo(copyOverlapBackward.AsSpan(0, 4), copyOverlapBackward.AsSpan(1, 4)));
            ExpectBool(
                "TryCopyTo overlap backward destination",
                true,
                StdMemoryExtensions.SequenceEqual(copyOverlapBackward, new char[] { 'a', 'a', 'b', 'c', 'd' }));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectInt(string scenario, int expected, int actual)
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
}
