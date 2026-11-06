using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

sealed class LibvirtOptions
{
    private static readonly string[] DefaultRequiredMarkers =
    {
        "OS_SMOKE_PASS",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY"
    };
    private readonly List<string> requiredMarkers = new List<string>();
    private readonly List<string> forbiddenMarkers = new List<string>();
    public string DomainName { get; private set; } = "os";
    public int MemoryMiB { get; private set; } = 256;
    public int VCpuCount { get; private set; } = 1;
    public string ImagePath { get; private set; } = "artifacts/images/os.raw";
    public string DiskFormat { get; private set; } = "raw";
    public string SerialLogPath { get; private set; } = "artifacts/logs/libvirt-serial.log";
    public string OvmfLoaderPath { get; private set; } = string.Empty;
    public LibvirtNetwork Network { get; private set; } = LibvirtNetwork.None;
    public int TimeoutSeconds { get; private set; } = 30;

    public IReadOnlyList<string> RequiredMarkers
    {
        get
        {
            return requiredMarkers.Count == 0 ? DefaultRequiredMarkers : requiredMarkers;
        }
    }

    public IReadOnlyList<string> ForbiddenMarkers => forbiddenMarkers;
    public bool GenerateXml
    {
        get; private set;
    }
    public bool PlanLifecycle
    {
        get; private set;
    }
    public bool ValidateXml
    {
        get; private set;
    }
    public bool CheckSerial
    {
        get; private set;
    }
    public bool AllowRealLifecycle
    {
        get; private set;
    }
    public bool EmitDiagnostics
    {
        get; private set;
    }
    public bool RequireExistingPaths
    {
        get; private set;
    }
    public bool ShowHelp
    {
        get; private set;
    }
    public string SummaryLogPath { get; private set; } = string.Empty;
    public string DomainXmlPath { get; private set; } = string.Empty;

    public static LibvirtOptions Parse(string[] args)
    {
        LibvirtOptions options = new LibvirtOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "generate-xml":
                case "--dry-run":
                    options.GenerateXml = true;
                    break;
                case "plan-lifecycle":
                case "--plan-lifecycle":
                    options.PlanLifecycle = true;
                    break;
                case "validate-xml":
                case "--validate-xml":
                    options.ValidateXml = true;
                    break;
                case "check-serial":
                case "--check-serial":
                    options.CheckSerial = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--name":
                    options.DomainName = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--memory-mib":
                    options.MemoryMiB = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--vcpu":
                case "--vcpus":
                    options.VCpuCount = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--image":
                    options.ImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--disk-format":
                    options.DiskFormat = ParseDiskFormat(ReadValue(args, ref i, arg));
                    break;
                case "--serial-log":
                    options.SerialLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--summary-log":
                    options.SummaryLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--domain-xml":
                case "--xml":
                    options.DomainXmlPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--ovmf-loader":
                case "--ovmf-code":
                    options.OvmfLoaderPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--network":
                    options.Network = ParseNetwork(ReadValue(args, ref i, arg));
                    break;
                case "--timeout-seconds":
                    options.TimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--require-marker":
                    options.requiredMarkers.Add(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--forbid-marker":
                    options.forbiddenMarkers.Add(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--diagnostics":
                    options.EmitDiagnostics = true;
                    break;
                case "--validate-paths":
                    options.RequireExistingPaths = true;
                    break;
                case "--allow-real-libvirt-lifecycle":
                    options.AllowRealLifecycle = true;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("OS.LibvirtRunner generate-xml [--name <domain>] [--memory-mib <MiB>] [--vcpu <count>] [--image <raw|qcow2 image>] [--disk-format raw|qcow2] [--serial-log <path>] [--ovmf-loader <OVMF_CODE.fd>] [--network none|default] [--require-marker <marker>]... [--forbid-marker <marker>]... [--diagnostics] [--validate-paths]");
        Console.WriteLine("OS.LibvirtRunner plan-lifecycle emits a plan-only real-libvirt lifecycle contract and never starts a domain.");
        Console.WriteLine("OS.LibvirtRunner validate-xml --domain-xml <path> validates generated domain XML without starting a domain.");
        Console.WriteLine("OS.LibvirtRunner check-serial validates an existing serial log against required and forbidden marker contracts without starting a domain.");
        Console.WriteLine("OS.LibvirtRunner --dry-run uses the same XML generation mode and does not require libvirt.");
        Console.WriteLine("--diagnostics writes LIBVIRT_DIAG lines, including required and forbidden serial markers, to stderr while preserving XML on stdout.");
        Console.WriteLine("--summary-log writes the check-serial marker summary to a file.");
        Console.WriteLine("--timeout-seconds records the future serial-marker polling deadline for lifecycle planning.");
        Console.WriteLine("--allow-real-libvirt-lifecycle records explicit future opt-in intent; real lifecycle execution is still not implemented.");
        Console.WriteLine("--validate-paths requires the image, serial log directory, and configured OVMF loader to exist.");
    }

    public void ValidatePaths()
    {
        if (!RequireExistingPaths)
        {
            return;
        }

        if (!File.Exists(ImagePath))
        {
            throw new ArgumentException("Image file does not exist: " + ImagePath);
        }

        long imageByteCount = GetImageByteCount(ImagePath);
        if (imageByteCount == 0)
        {
            throw new ArgumentException("Image file is empty: " + ImagePath);
        }

        string detectedFormat = DetectImageFormat(ImagePath);
        if (string.Equals(DiskFormat, "qcow2", StringComparison.Ordinal) && !string.Equals(detectedFormat, "qcow2", StringComparison.Ordinal))
        {
            throw new ArgumentException("Image disk format mismatch: expected qcow2 but detected " + detectedFormat + ": " + ImagePath);
        }

        if (string.Equals(DiskFormat, "raw", StringComparison.Ordinal) && string.Equals(detectedFormat, "qcow2", StringComparison.Ordinal))
        {
            throw new ArgumentException("Image disk format mismatch: expected raw but detected qcow2: " + ImagePath);
        }

        string serialLogDirectory = GetSerialLogDirectory();
        if (!Directory.Exists(serialLogDirectory))
        {
            throw new ArgumentException("Serial log directory does not exist: " + serialLogDirectory);
        }

        if (!string.IsNullOrWhiteSpace(OvmfLoaderPath) && !File.Exists(OvmfLoaderPath))
        {
            throw new ArgumentException("OVMF loader file does not exist: " + OvmfLoaderPath);
        }
    }

    public string GetImageByteCountDiagnostic()
    {
        if (!File.Exists(ImagePath))
        {
            return "missing";
        }

        try
        {
            return GetImageByteCount(ImagePath).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentException ex)
        {
            return "error=" + ex.Message;
        }
    }

    public string GetDetectedImageFormatDiagnostic()
    {
        if (!File.Exists(ImagePath))
        {
            return "not_checked";
        }

        try
        {
            return DetectImageFormat(ImagePath);
        }
        catch (ArgumentException ex)
        {
            return "error=" + ex.Message;
        }
    }

    public string GetSerialLogDirectory()
    {
        string? directory = Path.GetDirectoryName(SerialLogPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Directory.GetCurrentDirectory();
        }

        return directory;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for " + option);
        }

        index++;
        return args[index];
    }

    private static string RequireNonEmpty(string value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for " + option + " cannot be empty.");
        }

        return value;
    }

    private static long GetImageByteCount(string imagePath)
    {
        try
        {
            return new FileInfo(imagePath).Length;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new ArgumentException("could_not_read_image_length error=" + ex.GetType().Name);
        }
    }

    private static string DetectImageFormat(string imagePath)
    {
        try
        {
            Span<byte> header = stackalloc byte[4];
            using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            int bytesRead = stream.Read(header);
            if (bytesRead == 4 && header[0] == (byte)'Q' && header[1] == (byte)'F' && header[2] == (byte)'I' && header[3] == 0xFB)
            {
                return "qcow2";
            }

            return "raw-compatible";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new ArgumentException("could_not_read_image_header error=" + ex.GetType().Name);
        }
    }

    private static int ReadPositiveInt(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException("Value for " + option + " must be a positive integer.");
        }

        return parsed;
    }

    private static LibvirtNetwork ParseNetwork(string value)
    {
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return LibvirtNetwork.None;
        }

        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return LibvirtNetwork.Default;
        }

        throw new ArgumentException("Value for --network must be none or default.");
    }

    private static string ParseDiskFormat(string value)
    {
        if (string.Equals(value, "raw", StringComparison.OrdinalIgnoreCase))
        {
            return "raw";
        }

        if (string.Equals(value, "qcow2", StringComparison.OrdinalIgnoreCase))
        {
            return "qcow2";
        }

        throw new ArgumentException("Value for --disk-format must be raw or qcow2.");
    }
}
