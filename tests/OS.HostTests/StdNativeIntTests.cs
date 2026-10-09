using OS.Std;
using System.Runtime.CompilerServices;

internal static class StdNativeIntTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        RunCheckedArithmetic();
        RunAlignmentHelpers();
        RunDefaultResultContract();
    }

    private static void RunCheckedArithmetic()
    {
        const string name = "std.nativeint checked arithmetic";
        HostCheck.Run(name, () =>
        {
            NativeUIntOperationResult add = NativeInt.Add((nuint)0x1000, (nuint)0x20);
            NativeUIntOperationResult overflow = NativeInt.Add(nuint.MaxValue, 1);
            NativeUIntOperationResult subtract = NativeInt.Subtract((nuint)0x1000, (nuint)0x20);
            NativeUIntOperationResult underflow = NativeInt.Subtract((nuint)0x20, (nuint)0x1000);

            ExpectBool("status", true, NativeInt.Status == CompatibilityStatus.Partial);
            ExpectResult("add", NativeUIntOperationStatus.Success, (nuint)0x1020, add);
            ExpectResult("overflow", NativeUIntOperationStatus.Overflow, 0, overflow);
            ExpectResult("subtract", NativeUIntOperationStatus.Success, (nuint)0x0FE0, subtract);
            ExpectResult("underflow", NativeUIntOperationStatus.Underflow, 0, underflow);
        });
    }

    private static void RunAlignmentHelpers()
    {
        const string name = "std.nativeint alignment helpers";
        HostCheck.Run(name, () =>
        {
            ExpectBool("power of two", true, NativeInt.IsPowerOfTwo((nuint)4096));
            ExpectBool("zero power of two", false, NativeInt.IsPowerOfTwo(0));
            ExpectBool("non power of two", false, NativeInt.IsPowerOfTwo((nuint)24));
            ExpectBool("is aligned", true, NativeInt.IsAligned((nuint)0x2000, (nuint)0x1000));
            ExpectBool("is not aligned", false, NativeInt.IsAligned((nuint)0x2001, (nuint)0x1000));
            ExpectBool("invalid alignment is false", false, NativeInt.IsAligned((nuint)0x2000, (nuint)24));

            ExpectResult(
                "align up already aligned",
                NativeUIntOperationStatus.Success,
                (nuint)0x2000,
                NativeInt.AlignUp((nuint)0x2000, (nuint)0x1000));
            ExpectResult(
                "align up rounds",
                NativeUIntOperationStatus.Success,
                (nuint)0x3000,
                NativeInt.AlignUp((nuint)0x2001, (nuint)0x1000));
            ExpectResult(
                "align down",
                NativeUIntOperationStatus.Success,
                (nuint)0x2000,
                NativeInt.AlignDown((nuint)0x2FFF, (nuint)0x1000));
            ExpectResult(
                "align up invalid",
                NativeUIntOperationStatus.InvalidAlignment,
                0,
                NativeInt.AlignUp((nuint)0x2001, (nuint)24));
            ExpectResult(
                "align down invalid",
                NativeUIntOperationStatus.InvalidAlignment,
                0,
                NativeInt.AlignDown((nuint)0x2001, 0));
            ExpectResult(
                "align up overflow",
                NativeUIntOperationStatus.Overflow,
                0,
                NativeInt.AlignUp(nuint.MaxValue, (nuint)2));
        });
    }

    private static void RunDefaultResultContract()
    {
        const string name = "std.nativeint default result is not success";
        HostCheck.Run(name, () =>
        {
            NativeUIntOperationResult result = default;

            ExpectBool("default status", true, result.Status == NativeUIntOperationStatus.Success);
            ExpectBool("default initialized", false, result.IsInitialized);
            ExpectBool("default is not evidence", false, result.Succeeded);
            ExpectBool(
                "result contains no references",
                false,
                RuntimeHelpers.IsReferenceOrContainsReferences<NativeUIntOperationResult>());
        });
    }

    private static void ExpectResult(
        string scenario,
        NativeUIntOperationStatus expectedStatus,
        nuint expectedValue,
        NativeUIntOperationResult actual)
    {
        if (actual.Status != expectedStatus || actual.Value != expectedValue)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " value=" + expectedValue
                + " but returned status=" + actual.Status
                + " value=" + actual.Value + ".");
        }

        if ((expectedStatus == NativeUIntOperationStatus.Success) != actual.Succeeded)
        {
            throw new InvalidOperationException(scenario + " returned inconsistent success metadata.");
        }

        if (!actual.IsInitialized)
        {
            throw new InvalidOperationException(scenario + " returned uninitialized result metadata.");
        }
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }
}
