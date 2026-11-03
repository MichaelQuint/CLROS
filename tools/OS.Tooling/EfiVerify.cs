using System.Buffers.Binary;
using System.Globalization;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    public static int Run(string[] args)
    {
        const string name = "efi.artifact.verify";
        Console.WriteLine("TEST_START " + name);
        try
        {
            Verify(EfiOptions.Parse(args, false), Console.WriteLine);
            Console.WriteLine("TEST_PASS " + name);
            return 0;
        }
        catch (ToolFailure failure)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + failure.Message);
            return 1;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL " + name + " verifier_failed type=" + error.GetType().Name);
            return 1;
        }
    }

    public static void Verify(EfiOptions options, Action<string>? emit = null)
    {
        using var context = new EfiVerification(options, emit ?? (_ =>
        {
        }));
        context.Verify();
    }

    public static bool TestSdkExactVersion(string actual, string declared) => EfiContracts.IsPinnedSdkVersion(actual) && EfiContracts.IsPinnedSdkVersion(declared);
    /// <summary>Host-only copy-binding checks over borrowed synthetic map/image inputs.
    /// Throws ToolFailure on mismatched ownership, presence or executable backing;
    /// this does not verify provenance, callback behavior or guest execution.</summary>

    public static void TestLinuxCopyBindings(EfiProfile profile, string map, byte[] image) => EfiVerification.VerifyLinuxCopyBindings(profile, map, new EfiPeImage(image));
    /// <summary>Borrow bounded synthetic map/image inputs to check web/service
    /// callback selection, original object ownership and executable backing.
    /// Invalid bindings throw; this grants no native behavior or guest proof.</summary>

    public static void TestWebServiceBindings(EfiProfile profile, string map, byte[] image) => EfiVerification.VerifyWebServiceBindings(profile, map, new EfiPeImage(image));
    public static bool TestSingleFinallyEhTable(byte[]? table, ulong typeRva) => table is { Length: 11 } && typeRva is > 0 and <= uint.MaxValue && table.AsSpan(0, 7).SequenceEqual(new byte[] { 0x04, 0x36, 0x32, 0x8e, 0x36, 0xa0, 0xbc }) && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(7)) == typeRva;
    public static bool TestSingleFinallyUnwindBytes(byte[]? data, bool root, ulong tableRva)
    {
        ReadOnlySpan<byte> prefix = root ? [0x01, 0x06, 0x03, 0x00, 0x06, 0x62, 0x02, 0x30, 0x01, 0x50, 0x04] : [0x01, 0x04, 0x01, 0x00, 0x04, 0x42, 0x01];
        return data is not null && data.Length == (root ? 15 : 7) && data.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (!root || tableRva is > 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(11)) == tableRva);
    }
}
