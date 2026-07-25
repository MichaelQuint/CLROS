using System.Runtime.CompilerServices;
using OS.Std;
using StdArrayHelpers = OS.Std.ArrayHelpers;

internal static class StdArrayHelpersTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunClearFillUnmanagedSpans();
        RunIndexOfUnmanagedSpans();
        RunBinarySearchInsertionPoints();
        RunSortUnmanagedSpans();
        RunTrySortRangeAndStatus();
        RunTrySortDefaultResult();
        RunTryBinarySearchFoundRange();
        RunTryBinarySearchNotFoundRange();
        RunTryBinarySearchInvalidRange();
        RunTryBinarySearchDefaultResult();
    }

    private static void RunClearFillUnmanagedSpans()
    {
        const string name = "std.arrayhelpers clear fill unmanaged spans";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ExpectBool("status", true, StdArrayHelpers.Status == CompatibilityStatus.Partial);

            Span<byte> empty = Span<byte>.Empty;
            StdArrayHelpers.Clear(empty);
            StdArrayHelpers.Fill(empty, (byte)7);

            int[] values = new int[] { 1, -2, 3, int.MaxValue };
            StdArrayHelpers.Clear(values);
            ExpectInt("clear first", 0, values[0]);
            ExpectInt("clear second", 0, values[1]);
            ExpectInt("clear last", 0, values[3]);

            StdArrayHelpers.Fill(values.AsSpan(1, 2), 42);
            ExpectInt("fill preserves prefix", 0, values[0]);
            ExpectInt("fill writes first selected", 42, values[1]);
            ExpectInt("fill writes last selected", 42, values[2]);
            ExpectInt("fill preserves suffix", 0, values[3]);

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunIndexOfUnmanagedSpans()
    {
        const string name = "std.arrayhelpers indexof unmanaged spans";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ReadOnlySpan<ushort> empty = ReadOnlySpan<ushort>.Empty;
            ushort[] values = new ushort[] { 7, 3, 7, 9, 7 };

            ExpectInt("index empty", -1, StdArrayHelpers.IndexOf(empty, (ushort)7));
            ExpectInt("last index empty", -1, StdArrayHelpers.LastIndexOf(empty, (ushort)7));
            ExpectInt("index first duplicate", 0, StdArrayHelpers.IndexOf(values, (ushort)7));
            ExpectInt("index middle", 3, StdArrayHelpers.IndexOf(values, (ushort)9));
            ExpectInt("index missing", -1, StdArrayHelpers.IndexOf(values, (ushort)2));
            ExpectInt("last index duplicate", 4, StdArrayHelpers.LastIndexOf(values, (ushort)7));
            ExpectInt("last index middle", 1, StdArrayHelpers.LastIndexOf(values, (ushort)3));
            ExpectInt("last index missing", -1, StdArrayHelpers.LastIndexOf(values, (ushort)2));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunBinarySearchInsertionPoints()
    {
        const string name = "std.arrayhelpers binarysearch insertion points";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ReadOnlySpan<int> empty = ReadOnlySpan<int>.Empty;
            int[] sorted = new int[] { 1, 3, 5, 7 };
            int[] duplicates = new int[] { 1, 3, 3, 3, 9 };

            ExpectInt("empty insertion", -1, StdArrayHelpers.BinarySearch(empty, 5));
            ExpectInt("first", 0, StdArrayHelpers.BinarySearch(sorted, 1));
            ExpectInt("middle", 2, StdArrayHelpers.BinarySearch(sorted, 5));
            ExpectInt("last", 3, StdArrayHelpers.BinarySearch(sorted, 7));
            ExpectInt("below insertion", -1, StdArrayHelpers.BinarySearch(sorted, 0));
            ExpectInt("between insertion", -3, StdArrayHelpers.BinarySearch(sorted, 4));
            ExpectInt("after insertion", -5, StdArrayHelpers.BinarySearch(sorted, 9));

            int duplicateIndex = StdArrayHelpers.BinarySearch(duplicates, 3);
            ExpectBool("duplicate match in range", true, duplicateIndex >= 1 && duplicateIndex <= 3);
            ExpectInt("duplicate match value", 3, duplicates[duplicateIndex]);

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunSortUnmanagedSpans()
    {
        const string name = "std.arrayhelpers sort unmanaged spans";
        Console.WriteLine("TEST_START " + name);

        try
        {
            Span<int> empty = Span<int>.Empty;
            StdArrayHelpers.Sort(empty);

            int[] single = new int[] { 42 };
            StdArrayHelpers.Sort(single);
            ExpectInt("single no-op", 42, single[0]);

            int[] alreadySorted = new int[] { -3, 0, 5, 9 };
            StdArrayHelpers.Sort(alreadySorted);
            ExpectSorted("already sorted", alreadySorted, new int[] { -3, 0, 5, 9 });

            int[] reversed = new int[] { 9, 5, 0, -3 };
            StdArrayHelpers.Sort(reversed);
            ExpectSorted("reverse sorted", reversed, new int[] { -3, 0, 5, 9 });

            int[] duplicates = new int[] { 4, 1, 4, 2, 1, 3 };
            StdArrayHelpers.Sort(duplicates);
            ExpectSorted("duplicates sorted", duplicates, new int[] { 1, 1, 2, 3, 4, 4 });

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTrySortRangeAndStatus()
    {
        const string name = "std.arrayhelpers trysort range and status";
        Console.WriteLine("TEST_START " + name);

        try
        {
            int[] values = new int[] { 99, 8, 3, 8, 1, 77 };

            ArraySortResult sorted = StdArrayHelpers.TrySort(
                values,
                start: 1,
                length: 4);

            ExpectSortResult(
                "range sorted",
                ArraySortStatus.Success,
                1,
                4,
                values.Length,
                sorted);
            ExpectSorted("range sorted values", values, new int[] { 99, 1, 3, 8, 8, 77 });

            int[] invalid = new int[] { 4, 3, 2, 1 };
            ArraySortResult invalidRange = StdArrayHelpers.TrySort(
                invalid,
                start: 2,
                length: 3);

            ExpectSortResult(
                "invalid range",
                ArraySortStatus.InvalidRange,
                2,
                3,
                invalid.Length,
                invalidRange);
            ExpectSorted("invalid range does not write", invalid, new int[] { 4, 3, 2, 1 });

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTrySortDefaultResult()
    {
        const string name = "std.arrayhelpers trysort default result is not success";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ArraySortResult result = default;

            ExpectBool("default status", true, result.Status == ArraySortStatus.Success);
            ExpectBool("default initialized", false, result.IsInitialized);
            ExpectBool("default is not evidence", false, result.Succeeded);
            ExpectBool(
                "sort result contains no references",
                false,
                RuntimeHelpers.IsReferenceOrContainsReferences<ArraySortResult>());

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryBinarySearchFoundRange()
    {
        const string name = "std.arrayhelpers trybinarysearch found range";
        Console.WriteLine("TEST_START " + name);

        try
        {
            int[] sorted = new int[] { 1, 3, 5, 7, 9 };

            ArraySearchResult found = StdArrayHelpers.TryBinarySearch(
                sorted,
                start: 1,
                length: 3,
                value: 7);

            ExpectSearch(
                "found inside range",
                ArraySearchStatus.Success,
                3,
                3,
                1,
                3,
                sorted.Length,
                found);

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryBinarySearchNotFoundRange()
    {
        const string name = "std.arrayhelpers trybinarysearch notfound range";
        Console.WriteLine("TEST_START " + name);

        try
        {
            int[] sorted = new int[] { 1, 3, 5, 7, 9 };

            ArraySearchResult between = StdArrayHelpers.TryBinarySearch(
                sorted,
                start: 1,
                length: 3,
                value: 4);
            ArraySearchResult emptyTail = StdArrayHelpers.TryBinarySearch(
                sorted,
                start: sorted.Length,
                length: 0,
                value: 10);

            ExpectSearch(
                "between insertion",
                ArraySearchStatus.NotFound,
                -3,
                2,
                1,
                3,
                sorted.Length,
                between);
            ExpectSearch(
                "empty tail insertion",
                ArraySearchStatus.NotFound,
                -6,
                5,
                sorted.Length,
                0,
                sorted.Length,
                emptyTail);

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryBinarySearchInvalidRange()
    {
        const string name = "std.arrayhelpers trybinarysearch invalid range";
        Console.WriteLine("TEST_START " + name);

        try
        {
            int[] sorted = new int[] { 1, 3, 5 };

            ExpectSearch(
                "negative start",
                ArraySearchStatus.InvalidRange,
                -1,
                -1,
                -1,
                1,
                sorted.Length,
                StdArrayHelpers.TryBinarySearch(sorted, start: -1, length: 1, value: 1));
            ExpectSearch(
                "negative length",
                ArraySearchStatus.InvalidRange,
                -1,
                -1,
                0,
                -1,
                sorted.Length,
                StdArrayHelpers.TryBinarySearch(sorted, start: 0, length: -1, value: 1));
            ExpectSearch(
                "range too long",
                ArraySearchStatus.InvalidRange,
                -1,
                -1,
                2,
                2,
                sorted.Length,
                StdArrayHelpers.TryBinarySearch(sorted, start: 2, length: 2, value: 3));
            ExpectSearch(
                "start past end",
                ArraySearchStatus.InvalidRange,
                -1,
                -1,
                4,
                0,
                sorted.Length,
                StdArrayHelpers.TryBinarySearch(sorted, start: 4, length: 0, value: 5));

            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void RunTryBinarySearchDefaultResult()
    {
        const string name = "std.arrayhelpers trybinarysearch default result is not success";
        Console.WriteLine("TEST_START " + name);

        try
        {
            ArraySearchResult result = default;

            ExpectBool("default status", true, result.Status == ArraySearchStatus.Success);
            ExpectBool("default initialized", false, result.IsInitialized);
            ExpectBool("default is not evidence", false, result.Succeeded);
            ExpectBool(
                "result contains no references",
                false,
                RuntimeHelpers.IsReferenceOrContainsReferences<ArraySearchResult>());

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

    private static void ExpectSorted(string scenario, ReadOnlySpan<int> actual, ReadOnlySpan<int> expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException(
                scenario + " expected length " + expected.Length + " but returned " + actual.Length + ".");
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                throw new InvalidOperationException(
                    scenario + " expected value " + expected[i] + " at index " + i + " but returned " + actual[i] + ".");
            }
        }
    }

    private static void ExpectSortResult(
        string scenario,
        ArraySortStatus expectedStatus,
        int expectedStart,
        int expectedLength,
        int expectedSourceLength,
        ArraySortResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Start != expectedStart ||
            actual.Length != expectedLength ||
            actual.SourceLength != expectedSourceLength)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " start=" + expectedStart
                + " length=" + expectedLength
                + " sourceLength=" + expectedSourceLength
                + " but returned status=" + actual.Status
                + " start=" + actual.Start
                + " length=" + actual.Length
                + " sourceLength=" + actual.SourceLength + ".");
        }

        if ((expectedStatus == ArraySortStatus.Success) != actual.Succeeded)
        {
            throw new InvalidOperationException(scenario + " returned inconsistent success metadata.");
        }

        if (!actual.IsInitialized)
        {
            throw new InvalidOperationException(scenario + " returned uninitialized result metadata.");
        }
    }

    private static void ExpectSearch(
        string scenario,
        ArraySearchStatus expectedStatus,
        int expectedIndex,
        int expectedInsertionIndex,
        int expectedStart,
        int expectedLength,
        int expectedSourceLength,
        ArraySearchResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Index != expectedIndex ||
            actual.InsertionIndex != expectedInsertionIndex ||
            actual.Start != expectedStart ||
            actual.Length != expectedLength ||
            actual.SourceLength != expectedSourceLength)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " index=" + expectedIndex
                + " insertion=" + expectedInsertionIndex
                + " start=" + expectedStart
                + " length=" + expectedLength
                + " sourceLength=" + expectedSourceLength
                + " but returned status=" + actual.Status
                + " index=" + actual.Index
                + " insertion=" + actual.InsertionIndex
                + " start=" + actual.Start
                + " length=" + actual.Length
                + " sourceLength=" + actual.SourceLength + ".");
        }

        if ((expectedStatus == ArraySearchStatus.Success) != actual.Succeeded)
        {
            throw new InvalidOperationException(scenario + " returned inconsistent success metadata.");
        }

        if (!actual.IsInitialized)
        {
            throw new InvalidOperationException(scenario + " returned uninitialized result metadata.");
        }
    }
}
