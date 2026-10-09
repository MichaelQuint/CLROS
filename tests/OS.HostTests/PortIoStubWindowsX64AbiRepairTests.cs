using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using OS.Kernel.Diagnostics;

internal static class PortIoStubWindowsX64AbiRepairTests
{
    private const string TestName = "m02.out8-abi-repair";
    private const string ReadyMarker =
        "CLROS_M02_OUT8_ABI_REPAIR_READY " +
        "out8Bytes=88D06689CAEEC3 " +
        "fnv1a64=982E5B7E3EE58E56 " +
        "sha256=" + OS.Tooling.EfiContracts.PortIoStubPageSha256 + " " +
        "valueBeforePort=true in8Unchanged=true";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        HostCheck.Run(TestName, () =>
        {

            if (!TestExactWindowsX64ArgumentPreservationAndPageFingerprint())
            {
                throw new InvalidOperationException(
                    "The Out8 Windows x64 ABI repair contract does not match.");
            }

            Console.WriteLine(ReadyMarker);
        });
    }

    private static bool TestExactWindowsX64ArgumentPreservationAndPageFingerprint()
    {
        byte[] out8 = new byte[PortIoStubPlan.Out8RequiredByteLength];
        byte[] in8 = new byte[PortIoStubPlan.In8RequiredByteLength];
        PortIoStubPlanResult out8Result = PortIoStubPlan.Out8.Emit(out8);
        PortIoStubPlanResult in8Result = PortIoStubPlan.In8.Emit(in8);
        ReadOnlySpan<byte> correctedOut8 =
            [0x88, 0xd0, 0x66, 0x89, 0xca, 0xee, 0xc3];
        ReadOnlySpan<byte> clobberingOut8 =
            [0x66, 0x89, 0xca, 0x88, 0xd0, 0xee, 0xc3];
        ReadOnlySpan<byte> unchangedIn8 = [0x66, 0x89, 0xca, 0xec, 0xc3];

        if (!out8Result.Succeeded ||
            !in8Result.Succeeded ||
            !out8.AsSpan().SequenceEqual(correctedOut8) ||
            out8.AsSpan().SequenceEqual(clobberingOut8) ||
            !in8.AsSpan().SequenceEqual(unchangedIn8))
        {
            return false;
        }

        byte[] page = new byte[
            checked((int)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize)];
        ReadOnlySpan<byte> safe = [0xb8, 0x4b, 0x4f, 0x53, 0x4f, 0xc3];
        safe.CopyTo(page.AsSpan((int)ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset));
        out8.CopyTo(page.AsSpan((int)ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset));
        in8.CopyTo(page.AsSpan((int)ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset));

        ulong fnv1a64 = ExistingIn8Out8StubBytesRWPagesFinalize.Fnv1a64OffsetBasis;
        foreach (byte value in page)
        {
            fnv1a64 = unchecked(
                (fnv1a64 ^ value) *
                ExistingIn8Out8StubBytesRWPagesFinalize.Fnv1a64Prime);
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(page));
        return fnv1a64 == 0x982e_5b7e_3ee5_8e56 &&
            fnv1a64 == ExistingIn8Out8StubBytesRWPagesFinalize.ExpectedFnv1a64 &&
            string.Equals(
                sha256,
                OS.Tooling.EfiContracts.PortIoStubPageSha256,
                StringComparison.Ordinal);
    }
}
