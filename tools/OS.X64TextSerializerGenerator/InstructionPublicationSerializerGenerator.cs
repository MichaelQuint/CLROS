using System.Security.Cryptography;

namespace OS.X64TextSerializerGenerator;

public static class InstructionPublicationSerializerGenerator
{
    public const int ObjectSizeBytes = 336;
    public const string ObjectSha256 =
        "DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B";
    public const string ExportName = "CLROS_X64_SerializeInstructionPublication";

    public const uint TextCharacteristics = 0x60500020;
    public const uint ReadOnlyDataCharacteristics = 0x40300040;

    private static readonly byte[] TextBytes =
    [
        0x53,
        0xB8, 0x00, 0x00, 0x00, 0x00,
        0xB9, 0x00, 0x00, 0x00, 0x00,
        0x0F, 0xA2,
        0xB8, 0x43, 0x4C, 0x52, 0x53,
        0x5B,
        0xC3,
    ];

    private static readonly byte[] UnwindInfoBytes =
    [
        0x01, 0x01, 0x01, 0x00,
        0x01, 0x30, 0x00, 0x00,
    ];

    private static readonly byte[] RuntimeFunctionBytes =
    [
        0x00, 0x00, 0x00, 0x00,
        0x14, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    ];

    public static bool TryGenerate(
        Span<byte> destination,
        out int bytesWritten,
        out CoffWriteError error)
    {
        bytesWritten = 0;
        if (destination.Length < ObjectSizeBytes)
        {
            error = CoffWriteError.CapacityExceeded;
            return false;
        }

        byte[] completedObject = new byte[ObjectSizeBytes];
        Amd64CoffObjectDefinition definition = CreateDefinition();
        if (!Amd64CoffWriter.TryWrite(
                in definition,
                completedObject,
                out int generatedSize,
                out error))
        {
            return false;
        }

        if (generatedSize != ObjectSizeBytes ||
            !string.Equals(
                Convert.ToHexString(SHA256.HashData(completedObject)),
                ObjectSha256,
                StringComparison.Ordinal))
        {
            error = CoffWriteError.InternalInvariant;
            return false;
        }

        completedObject.CopyTo(destination);
        bytesWritten = completedObject.Length;
        error = CoffWriteError.None;
        return true;
    }

    public static bool TryGenerate(
        int maximumObjectSizeBytes,
        out byte[] objectBytes,
        out CoffWriteError error)
    {
        objectBytes = [];
        if (maximumObjectSizeBytes < ObjectSizeBytes)
        {
            error = CoffWriteError.CapacityExceeded;
            return false;
        }

        byte[] candidate = new byte[ObjectSizeBytes];
        if (!TryGenerate(candidate, out int bytesWritten, out error) ||
            bytesWritten != candidate.Length)
        {
            return false;
        }

        objectBytes = candidate;
        error = CoffWriteError.None;
        return true;
    }

    private static Amd64CoffObjectDefinition CreateDefinition()
    {
        CoffSectionDefinition[] sections =
        [
            new(
                ".text$clrossm",
                (byte[])TextBytes.Clone(),
                TextCharacteristics,
                []),
            new(
                ".xdata$clrossm",
                (byte[])UnwindInfoBytes.Clone(),
                ReadOnlyDataCharacteristics,
                []),
            new(
                ".pdata$clrossm",
                (byte[])RuntimeFunctionBytes.Clone(),
                ReadOnlyDataCharacteristics,
                [
                    new(0, 0, Amd64CoffWriter.ImageRelAmd64Addr32Nb),
                    new(4, 0, Amd64CoffWriter.ImageRelAmd64Addr32Nb),
                    new(8, 1, Amd64CoffWriter.ImageRelAmd64Addr32Nb),
                ]),
        ];

        CoffSymbolDefinition[] symbols =
        [
            new(
                ExportName,
                0,
                1,
                0x20,
                Amd64CoffWriter.ImageSymClassExternal),
            new(
                ".xdata$clrossm",
                0,
                2,
                0,
                Amd64CoffWriter.ImageSymClassStatic),
        ];

        return new(
            Amd64CoffWriter.Amd64Machine,
            0,
            sections,
            symbols);
    }
}
