using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal static class NativePayloadBuilder
{
    private const int HeaderSize = 32;
    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        FileInfo input = ValidateInputPath(options.InputPath);
        string outputPath = ValidateOutputPath(options.OutputPath);
        byte[] inputBytes = File.ReadAllBytes(input.FullName);
        byte[] payload = CreatePayload(inputBytes);
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(outputPath, payload);
        output.WriteLine("NATIVE_PAYLOAD_BYTES " + payload.Length);
        output.WriteLine("NATIVE_PAYLOAD_ENTRY_OFFSET " + HeaderSize);
        output.WriteLine("NATIVE_PAYLOAD_ARTIFACT " + options.OutputPath);
    }

    public static byte[] CreatePayload(byte[] inputBytes)
    {
        byte[] payload = new byte[checked(HeaderSize + inputBytes.Length)];
        WriteAscii(payload, 0, "OSAPPv0");
        payload[7] = 0;
        WriteUInt16(payload, 8, 0);
        WriteUInt16(payload, 10, HeaderSize);
        WriteUInt32(payload, 12, 0);
        WriteUInt64(payload, 16, HeaderSize);
        WriteUInt64(payload, 24, 0);
        inputBytes.CopyTo(payload.AsSpan(HeaderSize));
        return payload;
    }

    public static FileInfo ValidateInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input path is required.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input path is invalid: " + inputPath);
        }

        if (!file.Exists)
        {
            throw new ImageBuilderException(21, "native_payload_input_not_found", "Native payload input was not found: " + inputPath);
        }

        if (file.Length == 0)
        {
            throw new ImageBuilderException(22, "native_payload_input_empty", "Native payload input is empty: " + inputPath);
        }

        return file;
    }

    public static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is empty.");
        }

        try
        {
            return Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(7, "invalid_output_path", "Output path is invalid: " + outputPath);
        }
    }

    private static void WriteAscii(byte[] bytes, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            bytes[offset + i] = (byte)value[i];
        }
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
    }
}
