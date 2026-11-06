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

internal static partial class OsPackageBuilder
{
    private const int HeaderSize = 32;
    private const int FileTableEntrySize = 32;
    private const int MaximumFileCount = 128;
    private const int MaximumPathLength = 255;
    public static void Build(ImageBuilderOptions options, TextWriter output)
    {
        DirectoryInfo inputDirectory = ValidateInputDirectory(options.InputPath);
        string outputPath = ValidateOutputPath(options.OutputPath);
        PackageSourceFile[] files = LoadSourceFiles(inputDirectory);
        byte[] package = CreatePackage(files);
        ValidatePackageSize(package);
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(outputPath, package);
        output.WriteLine("PACKAGE_FILE_COUNT " + files.Length);
        output.WriteLine("PACKAGE_BYTES " + package.Length);
        output.WriteLine("PACKAGE_ARTIFACT " + options.OutputPath);
    }

    public static byte[] CreateSingleFilePackage(string packagePath, byte[] content)
    {
        if (string.IsNullOrEmpty(packagePath))
        {
            throw new ImageBuilderException(24, "manifest_launch_plan_invalid", "Native manifest launch plan did not expose a package-relative app.path.");
        }

        byte[] pathBytes = Encoding.ASCII.GetBytes(packagePath);
        PackageSourceFile[] files = new[]
        {
            new PackageSourceFile(packagePath, pathBytes, content)
        };
        byte[] package = CreatePackage(files);
        ValidatePackageSize(package);
        return package;
    }

    public static byte[] CreatePackageFromDirectory(string inputPath)
    {
        DirectoryInfo inputDirectory = ValidateInputDirectory(inputPath);
        PackageSourceFile[] files = LoadSourceFiles(inputDirectory);
        byte[] package = CreatePackage(files);
        ValidatePackageSize(package);
        return package;
    }

    private static void ValidatePackageSize(byte[] package)
    {
        if (package.Length > ImageBuilderOptions.PackageMaxBytes)
        {
            throw new ImageBuilderException(20, "package_output_too_large", "Package output is larger than 16 MiB.");
        }
    }

    private static DirectoryInfo ValidateInputDirectory(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory is required.");
        }

        DirectoryInfo directory;
        try
        {
            directory = new DirectoryInfo(inputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory path is invalid: " + inputPath);
        }

        if (!directory.Exists)
        {
            throw new ImageBuilderException(16, "package_input_not_found", "Package input directory was not found: " + inputPath);
        }

        return directory;
    }

    private static string ValidateOutputPath(string outputPath)
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

    private static PackageSourceFile[] LoadSourceFiles(DirectoryInfo inputDirectory)
    {
        EnumerationOptions enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = 0
        };
        List<PackageSourceFile> files = new List<PackageSourceFile>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.Ordinal);
        string root = inputDirectory.FullName;
        foreach (string filePath in Directory.EnumerateFiles(root, "*", enumeration))
        {
            FileInfo file = new FileInfo(filePath);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ImageBuilderException(18, "package_input_unsafe_path", "Package input file is a reparse point: " + filePath);
            }

            string packagePath = NormalizePackagePath(root, file.FullName);
            if (!IsSafePackagePath(packagePath))
            {
                throw new ImageBuilderException(18, "package_input_unsafe_path", "Package input file has an unsafe package path: " + packagePath);
            }

            if (!seenPaths.Add(packagePath))
            {
                throw new ImageBuilderException(19, "package_input_duplicate_path", "Package input has a duplicate normalized path: " + packagePath);
            }

            byte[] content = File.ReadAllBytes(file.FullName);
            files.Add(new PackageSourceFile(packagePath, Encoding.ASCII.GetBytes(packagePath), content));
        }

        files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        if (files.Count == 0)
        {
            throw new ImageBuilderException(17, "package_input_empty", "Package input directory contains no files.");
        }

        if (files.Count > MaximumFileCount)
        {
            throw new ImageBuilderException(20, "package_output_too_large", "Package file count exceeds " + MaximumFileCount + ".");
        }

        return files.ToArray();
    }

    private static string NormalizePackagePath(string root, string filePath)
    {
        string relativePath = Path.GetRelativePath(root, filePath);
        return relativePath.Replace(Path.DirectorySeparatorChar.ToString(), "/", StringComparison.Ordinal).Replace(Path.AltDirectorySeparatorChar.ToString(), "/", StringComparison.Ordinal);
    }

    private static bool IsSafePackagePath(string path)
    {
        if (path.Length == 0 || path.Length > MaximumPathLength)
        {
            return false;
        }

        if (path[0] == '/' || path[^1] == '/' || path.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = path.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (int index = 0; index < path.Length; index++)
        {
            char value = path[index];
            if (value < 0x20 || value >= 0x7F || value == '\\' || value == ':' || value == '*' || value == '?' || value == '"' || value == '<' || value == '>' || value == '|')
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] CreatePackage(PackageSourceFile[] files)
    {
        int tableOffset = HeaderSize;
        int tableLength = checked(files.Length * FileTableEntrySize);
        int pathOffset = checked(tableOffset + tableLength);
        int contentOffset = pathOffset;
        foreach (PackageSourceFile file in files)
        {
            contentOffset = checked(contentOffset + file.PathBytes.Length);
        }

        int totalLength = contentOffset;
        foreach (PackageSourceFile file in files)
        {
            totalLength = checked(totalLength + file.Content.Length);
        }

        byte[] package = new byte[totalLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, 0);
        WriteUInt16(package, 10, HeaderSize);
        WriteUInt16(package, 12, files.Length);
        WriteUInt16(package, 14, FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        int currentPathOffset = pathOffset;
        int currentContentOffset = contentOffset;
        for (int index = 0; index < files.Length; index++)
        {
            PackageSourceFile file = files[index];
            int entryOffset = tableOffset + index * FileTableEntrySize;
            WriteUInt32(package, entryOffset, currentPathOffset);
            WriteUInt16(package, entryOffset + 4, file.PathBytes.Length);
            WriteUInt16(package, entryOffset + 6, 0);
            WriteUInt64(package, entryOffset + 8, (ulong)currentContentOffset);
            WriteUInt64(package, entryOffset + 16, (ulong)file.Content.Length);
            WriteUInt64(package, entryOffset + 24, 0);
            file.PathBytes.CopyTo(package.AsSpan(currentPathOffset, file.PathBytes.Length));
            file.Content.CopyTo(package.AsSpan(currentContentOffset, file.Content.Length));
            currentPathOffset += file.PathBytes.Length;
            currentContentOffset += file.Content.Length;
        }

        return package;
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

    private static void WriteUInt32(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)value);
    }

    private static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
    }
}
