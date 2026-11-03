using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

sealed class RunnerOptions
{
    private static readonly string[] DefaultRequiredMarkers = OS.Tooling.QemuContracts.RunnerDefaultRequiredMarkers.ToArray();
    private static readonly string[] DefaultForbiddenMarkers =
    {
        "OS_SMOKE_FAIL",
        "TEST_FAIL"
    };
    private const string DefaultWorkloadProfile = OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
    private readonly List<string> requiredMarkers = new List<string>(DefaultRequiredMarkers);
    private readonly List<string> forbiddenMarkers = new List<string>(DefaultForbiddenMarkers);
    private readonly List<string> markerPresets = new List<string>();
    private readonly List<string> orderedMarkers = new List<string>();
    private readonly List<string> uniqueMarkers = new List<string>();
    private bool markerConfigurationStarted;
    private bool defaultRequiredMarkersReplaced;
    public string QemuPath { get; private set; } = "qemu-system-x86_64";
    public string Accelerator { get; private set; } = OS.Tooling.QemuContracts.DefaultAccelerator;

    private bool acceleratorSpecified;
    public string ImagePath { get; private set; } = Path.Combine("artifacts", "images", "os.raw");
    public string DiskFormat { get; private set; } = "raw";
    public string OvmfCodePath { get; private set; } = string.Empty;
    public string SerialLogPath { get; private set; } = Path.Combine("artifacts", "logs", "qemu-smoke.log");
    public string SummaryLogPath { get; private set; } = string.Empty;
    public string Memory { get; private set; } = "256M";
    public string? FirmwareRngSource { get; private set; }
    public bool PciBarTestDevice { get; private set; }
    public bool VirtioNegotiationDevice { get; private set; }
    public bool ArpReceiveFixture { get; private set; }
    public bool ContinuingNetworkPeer { get; private set; }
    public bool SocketContainmentPeer { get; private set; }
    public int? SocketForwardPort { get; private set; }
    public bool ContinuingWeb { get; private set; }
    public bool FiniteStaticFiles { get; private set; }
    public bool FullDiskStaticFiles { get; private set; }
    public bool DiskReadTiming { get; private set; }
    public bool DiskStaticFiles => FiniteStaticFiles && ReadOnlyWebRootDisk;
    public bool ReadOnlyWebRootDisk { get; private set; }
    public StaticFileProcessEvidence? StaticEvidence { get; private set; }
    public string? WebRacePipe { get; private set; }
    public bool WebEventOwnedSignalRace { get; private set; }
    public bool ContinuingService { get; private set; }
    public bool ServiceQuotaFailure { get; private set; }
    public long SerialByteCapacity => FullDiskStaticFiles ? OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumSerialBytes : ContinuingService ? SerialMarkerStateMachine.ContinuingServiceByteCapacity : SerialMarkerStateMachine.DefaultByteCapacity;
    public ServiceEvidence? ServiceEvidence { get; private set; }
    public string SuccessTerminalMarker => ServiceQuotaFailure ? ServiceQuotaCapture.Terminal : FiniteStaticFiles || ReadOnlyWebRootDisk ? "TEST_PASS m16-012" : ContinuingService ? "TEST_PASS m16-012" : ContinuingWeb ? "TEST_PASS m10.004" : SerialMarkerStateMachine.DefaultSuccessTerminalMarker;
    public HttpProbeOptions? WebConfigProbe { get; private set; }
    public HttpProbeOptions? WebLogProbe { get; private set; }
    public HttpProbeOptions? WebLogRepeatProbe { get; private set; }
    public HttpProbeOptions? WebVectorProbe { get; private set; }
    public ContinuingWebEvidence? WebEvidence { get; private set; }
    public string WebLogCase { get; private set; } = "normal";

    private bool webLogCaseConfigured;
    private string? webConfigBody;
    public string? EthernetCapturePath { get; private set; }
    public bool VirtioBlockQueueDevice { get; private set; }
    public string? VirtioBlockBootImagePath { get; private set; }
    public string? WritableDataImagePath { get; private set; }
    public WritableDataImageLease? WritableDataLease { get; set; }
    public string WorkloadProfile { get; private set; } = DefaultWorkloadProfile;
    public IReadOnlyList<string> RequiredMarkers => requiredMarkers;
    public IReadOnlyList<string> ForbiddenMarkers => forbiddenMarkers;
    public IReadOnlyList<string> MarkerPresets => markerPresets;
    public IReadOnlyList<string> OrderedMarkers => orderedMarkers;
    public IReadOnlyList<string> UniqueMarkers => uniqueMarkers;
    public int TimeoutSeconds { get; private set; } = 30;
    public int StartupTimeoutSeconds { get; private set; } = 30;
    public bool WatchStartup => !ExpectedUnexpectedVectorPanic && !ExpectedOwnedSubsystemPanic && !ExpectedUnhandledException && (RequiredMarkers.Contains(QemuStartupDeadline.ReadyMarker) || OrderedMarkers.Contains(QemuStartupDeadline.ReadyMarker) || UniqueMarkers.Contains(QemuStartupDeadline.ReadyMarker));
    public HttpProbeOptions? HttpProbe { get; private set; }
    public bool ExpectedUnexpectedVectorPanic { get; private set; }
    public bool ExpectedOwnedSubsystemPanic { get; private set; }
    public bool ExpectedUnhandledException { get; private set; }
    public bool DryRun { get; private set; }
    public bool Preflight { get; private set; }
    public bool ShowHelp { get; private set; }

    private bool httpProbeConfigured;
    private string? httpProbeUrl;
    private int httpProbeExpectedStatusCode = 200;
    private string httpProbeExpectedBody = string.Empty;
    private bool httpProbeExpectedBodyConfigured;
    private int httpProbeTimeoutSeconds = 10;
    public static RunnerOptions Parse(string[] args)
    {
        RunnerOptions options = new RunnerOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--expect-unexpected-vector-panic":
                    if (options.ExpectedUnexpectedVectorPanic)
                        throw new ArgumentException("--expect-unexpected-vector-panic may be specified only once.");
                    options.ExpectedUnexpectedVectorPanic = true;
                    break;
                case "--expect-owned-subsystem-panic":
                    if (options.ExpectedOwnedSubsystemPanic)
                        throw new ArgumentException("--expect-owned-subsystem-panic may be specified only once.");
                    options.ExpectedOwnedSubsystemPanic = true;
                    break;
                case "--expect-unhandled-exception":
                    if (options.ExpectedUnhandledException)
                        throw new ArgumentException("--expect-unhandled-exception may be specified only once.");
                    options.ExpectedUnhandledException = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "preflight":
                case "--preflight":
                    options.Preflight = true;
                    break;
                case "--qemu":
                    options.QemuPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--accelerator":
                    if (options.acceleratorSpecified)
                        throw new ArgumentException("--accelerator may be specified only once.");
                    options.acceleratorSpecified = true;
                    string accelerator = ReadValue(args, ref i, arg);
                    if (accelerator is not ("whpx" or "tcg"))
                        throw new ArgumentException("qemu_accelerator_invalid");
                    options.Accelerator = accelerator;
                    break;
                case "--image":
                    options.ImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--disk-format":
                    options.SetDiskFormat(ReadValue(args, ref i, arg));
                    break;
                case "--ovmf-code":
                    options.OvmfCodePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--serial-log":
                    options.SerialLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--summary-log":
                    options.SummaryLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--pci-bar-test-device":
                    if (options.PciBarTestDevice)
                        throw new ArgumentException("--pci-bar-test-device may be specified only once.");
                    options.PciBarTestDevice = true;
                    break;
                case "--virtio-negotiation-device":
                    if (options.VirtioNegotiationDevice)
                        throw new ArgumentException("--virtio-negotiation-device may be specified only once.");
                    options.VirtioNegotiationDevice = true;
                    break;
                case "--ethernet-capture":
                    if (options.EthernetCapturePath is not null)
                        throw new ArgumentException("--ethernet-capture may be specified only once.");
                    options.EthernetCapturePath = Path.GetFullPath(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    if (options.EthernetCapturePath.IndexOfAny([',', '\r', '\n']) >= 0 || File.Exists(options.EthernetCapturePath))
                        throw new ArgumentException("--ethernet-capture requires a fresh file path without QEMU option separators.");
                    break;
                case "--arp-receive-fixture":
                    if (options.ArpReceiveFixture)
                        throw new ArgumentException("--arp-receive-fixture may be specified only once.");
                    options.ArpReceiveFixture = true;
                    break;
                case "--continuing-network-peer":
                    if (options.ContinuingNetworkPeer)
                        throw new ArgumentException("--continuing-network-peer may be specified only once.");
                    options.ContinuingNetworkPeer = true;
                    break;
                case "--socket-containment-peer":
                    if (options.SocketContainmentPeer)
                        throw new ArgumentException("--socket-containment-peer may be specified only once.");
                    options.SocketContainmentPeer = true;
                    break;
                case "--socket-forward-port":
                    if (options.SocketForwardPort is not null)
                        throw new ArgumentException("--socket-forward-port may be specified only once.");
                    string socketPort = ReadValue(args, ref i, arg);
                    if (!int.TryParse(socketPort, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
                        throw new ArgumentException("--socket-forward-port must be a decimal port from 1 through 65535.");
                    options.SocketForwardPort = port;
                    break;
                case "--continuing-web":
                    if (options.ContinuingWeb)
                        throw new ArgumentException("--continuing-web may be specified only once.");
                    options.ContinuingWeb = true;
                    break;
                case "--full-disk-static-files":
                    if (options.FullDiskStaticFiles)
                        throw new ArgumentException("duplicate full disk static selection");
                    options.FullDiskStaticFiles = true;
                    break;
                case "--disk-read-timing":
                    if (options.DiskReadTiming)
                        throw new ArgumentException("--disk-read-timing may be specified only once.");
                    options.DiskReadTiming = true;
                    break;
                case "--finite-static-files":
                    if (options.FiniteStaticFiles)
                        throw new ArgumentException("--finite-static-files may be specified only once.");
                    options.FiniteStaticFiles = true;
                    break;
                case "--readonly-webroot-disk":
                    if (options.ReadOnlyWebRootDisk)
                        throw new ArgumentException("--readonly-webroot-disk may be specified only once.");
                    options.ReadOnlyWebRootDisk = true;
                    break;
                case "--web-race-pipe":
                    if (options.WebRacePipe is not null)
                        throw new ArgumentException("--web-race-pipe may be specified only once.");
                    options.WebRacePipe = ReadValue(args, ref i, arg);
                    break;
                case "--web-event-owned-signal-race":
                    if (options.WebEventOwnedSignalRace)
                        throw new ArgumentException("--web-event-owned-signal-race may be specified only once.");
                    options.WebEventOwnedSignalRace = true;
                    break;
                case "--service-quota-failure":
                    if (options.ServiceQuotaFailure)
                        throw new ArgumentException("--service-quota-failure may be specified only once.");
                    options.ServiceQuotaFailure = true;
                    break;
                case "--continuing-service":
                    if (options.ContinuingService)
                        throw new ArgumentException("--continuing-service may be specified only once.");
                    options.ContinuingService = true;
                    break;
                case "--web-config-body":
                    if (options.webConfigBody is not null)
                        throw new ArgumentException("--web-config-body may be specified only once.");
                    options.webConfigBody = ReadValue(args, ref i, arg);
                    break;
                case "--web-log-case":
                    if (options.webLogCaseConfigured)
                        throw new ArgumentException("--web-log-case may be specified only once.");
                    options.webLogCaseConfigured = true;
                    options.WebLogCase = ReadValue(args, ref i, arg);
                    if (options.WebLogCase is not ("normal" or "records64"))
                        throw new ArgumentException("--web-log-case must be normal or records64.");
                    break;
                case "--virtio-block-boot-image":
                    if (options.VirtioBlockBootImagePath is not null)
                        throw new ArgumentException("--virtio-block-boot-image may be specified only once.");
                    options.VirtioBlockBootImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--writable-data-image":
                    if (options.WritableDataImagePath is not null)
                        throw new ArgumentException("--writable-data-image may be specified only once.");
                    options.WritableDataImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    if (options.WritableDataImagePath.IndexOfAny([',', '\r', '\n', '\0']) >= 0)
                        throw new ArgumentException("--writable-data-image forbids QEMU option separators.");
                    break;
                case "--virtio-block-queue-device":
                    if (options.VirtioBlockQueueDevice)
                        throw new ArgumentException("--virtio-block-queue-device may be specified only once.");
                    options.VirtioBlockQueueDevice = true;
                    break;
                case "--firmware-rng-source":
                    if (options.FirmwareRngSource is not null)
                        throw new ArgumentException("--firmware-rng-source may be specified only once.");
                    string rngSource = ReadValue(args, ref i, arg);
                    if (!string.Equals(rngSource, "virtio", StringComparison.Ordinal) && !string.Equals(rngSource, "missing", StringComparison.Ordinal))
                        throw new ArgumentException("--firmware-rng-source must be virtio or missing.");
                    options.FirmwareRngSource = rngSource;
                    break;
                case "--memory":
                    options.Memory = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--workload-profile":
                    options.WorkloadProfile = NormalizeWorkloadProfile(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--replace-default-required-markers":
                    options.ReplaceDefaultRequiredMarkers();
                    break;
                case "--require-marker":
                    options.AddRequiredMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--require-ordered-marker":
                    options.AddOrderedMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--require-unique-marker":
                    options.AddUniqueMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--marker-preset":
                    options.AddMarkerPreset(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--forbid-marker":
                    options.AddForbiddenMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--timeout":
                    options.TimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--startup-timeout":
                    options.StartupTimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--http-probe-url":
                    options.SetHttpProbeUrl(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-expected-status":
                    options.SetHttpProbeExpectedStatusCode(ReadHttpStatusCode(args, ref i, arg));
                    break;
                case "--http-probe-expected-body":
                    options.SetHttpProbeExpectedBody(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-timeout":
                    options.SetHttpProbeTimeoutSeconds(ReadPositiveInt(args, ref i, arg));
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        if (options.VirtioBlockBootImagePath is not null && (!options.VirtioBlockQueueDevice || options.DiskFormat != "raw"))
            throw new ArgumentException("--virtio-block-boot-image requires the dedicated raw Virtio block fixture.");
        options.FinishParsing();
        if (options.WritableDataImagePath is not null && (options.DiskFormat != "raw" || !options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.EthernetCapturePath is null || string.IsNullOrWhiteSpace(options.SummaryLogPath) || options.WorkloadProfile != "SingleMinimalApi" || !options.defaultRequiredMarkersReplaced || options.RequiredMarkers.Count == 0 || options.MarkerPresets.Count != 0 || options.TimeoutSeconds is < 120 or > 295 || options.ReadOnlyWebRootDisk || options.VirtioBlockBootImagePath is not null || options.VirtioBlockQueueDevice || options.PciBarTestDevice || options.ContinuingWeb || options.ContinuingService || options.ServiceQuotaFailure || options.FiniteStaticFiles || options.FullDiskStaticFiles || options.DiskReadTiming || options.ContinuingNetworkPeer || options.SocketContainmentPeer || options.SocketForwardPort is not null || options.HttpProbe is not null || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException || options.ImagePath.IndexOfAny([',', '\r', '\n', '\0']) >= 0))
            throw new ArgumentException("--writable-data-image requires the finite captured raw mechanical fixture, SingleMinimalApi, summary, replaced explicit markers and a120..295-second deadline without another fixture.");
        if (options.DiskReadTiming && (!options.FullDiskStaticFiles || !options.DiskStaticFiles || options.TimeoutSeconds is < 240 or > 600))
            throw new ArgumentException("--disk-read-timing requires the full finite readonly disk parent and explicit240..600-second deadline");
        if (options.FullDiskStaticFiles && (!options.DiskStaticFiles || (!options.DiskReadTiming && options.TimeoutSeconds is < 6600 or > 7200)))
            throw new ArgumentException("--full-disk-static-files requires the finite readonly disk parent and explicit6600..7200-second deadline");
        if (options.ReadOnlyWebRootDisk && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.DiskFormat != "raw" || options.VirtioBlockQueueDevice || options.VirtioBlockBootImagePath is not null || options.PciBarTestDevice || options.ContinuingNetworkPeer || options.SocketContainmentPeer || options.ContinuingWeb || options.ContinuingService || options.HttpProbe is not null || (options.DiskReadTiming ? options.TimeoutSeconds is < 240 or > 600 : options.FullDiskStaticFiles ? options.TimeoutSeconds is < 6600 or > 7200 : options.FiniteStaticFiles ? options.TimeoutSeconds is < 600 or > 825 : options.TimeoutSeconds is < 120 or > 295) || !options.defaultRequiredMarkersReplaced || string.IsNullOrWhiteSpace(options.SummaryLogPath)))
            throw new ArgumentException("--readonly-webroot-disk requires the finite captured raw disk/network child without another fixture.");
        if ((options.PciBarTestDevice ? 1 : 0) + (options.VirtioNegotiationDevice ? 1 : 0) + (options.VirtioBlockQueueDevice ? 1 : 0) > 1)
            throw new ArgumentException("Dedicated PCI test devices cannot share slot 6.");
        if (options.EthernetCapturePath is not null && !options.VirtioNegotiationDevice)
            throw new ArgumentException("--ethernet-capture requires the dedicated modern network fixture.");
        if (options.ArpReceiveFixture && (!options.VirtioNegotiationDevice || options.EthernetCapturePath is null))
            throw new ArgumentException("--arp-receive-fixture requires the captured dedicated modern network fixture.");
        if (options.ContinuingNetworkPeer && (!options.ArpReceiveFixture || options.TimeoutSeconds > 295 || options.HttpProbe is not null || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--continuing-network-peer requires the captured ARP fixture, a timeout at most 295 seconds and the ordinary diagnostic exit path.");
        if (options.SocketContainmentPeer && (!options.ArpReceiveFixture || options.TimeoutSeconds is < 120 or > 295 || options.ContinuingNetworkPeer || options.ContinuingWeb || options.ContinuingService || options.HttpProbe is not null || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--socket-containment-peer requires the captured ARP fixture, a120..295-second timeout and ordinary diagnostic exit without another peer.");
        if (options.SocketForwardPort is not null && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.EthernetCapturePath is null || options.ContinuingNetworkPeer || options.SocketContainmentPeer || (options.HttpProbe is not null && !options.ContinuingWeb) || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--socket-forward-port requires the captured dedicated modern ARP network fixture and ordinary diagnostic exit path, without --continuing-network-peer or HTTP probing.");
        options.ValidateContinuingWeb();
        options.ValidateContinuingService();
        if (options.FiniteStaticFiles && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.EthernetCapturePath is null || options.SocketForwardPort is null || string.IsNullOrWhiteSpace(options.SummaryLogPath) || options.WorkloadProfile != "SingleMinimalApi" || (!options.FullDiskStaticFiles && options.TimeoutSeconds is < 600 or > 900) || !options.defaultRequiredMarkersReplaced || options.MarkerPresets.Count != 0 || options.ContinuingWeb || options.ContinuingService || options.ContinuingNetworkPeer || options.SocketContainmentPeer || options.HttpProbe is not null || options.ServiceQuotaFailure))
            throw new ArgumentException("--finite-static-files requires captured forwarded modern networking, SingleMinimalApi, a600..900-second deadline and explicit finite markers without another application peer.");
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("--writable-data-image <raw> attaches a distinct existing mutable image at modern slot7 beside the captured slot6 NIC; --image boots through an AHCI temporary overlay. Requires raw, SingleMinimalApi, explicit replaced markers, summary and120..295seconds. Working identity/length stays held through actual child join; bytes may change. Unconfirmed cleanup retains the runner, never guest acceptance.");
        Console.WriteLine("--socket-containment-peer selects the finite three-socket timeout/VLAN raw Ethernet peer; requires captured modern ARP networking and a120..295-second timeout. Peer completion does not establish guest acceptance.");
        Console.WriteLine("--finite-static-files captures the finite static-file profile through guest shutdown, transport drain and final parent cleanup; external HTTP checks belong to the owning tooling peer.");
        Console.WriteLine("--disk-read-timing requires --full-disk-static-files, --finite-static-files, --readonly-webroot-disk and a240..600-second deadline. Its owning tooling peer joins deliberate cancellation after diagnostic HTTP checks; this does not qualify full service or cleanup.");
        Console.WriteLine("--continuing-network-peer uses a bounded loopback Ethernet peer for the captured M07-026 fixture; dry-run prints its ephemeral-port placeholder.");
        Console.WriteLine("--socket-forward-port <1-65535> forwards loopback TCP to guest port 8080 on the captured dedicated modern ARP network fixture, using QEMU usernet without the scripted Ethernet peer.");
        Console.WriteLine("--continuing-web requires that forward, exact loopback /healthz HTTP 200/body probing, --web-config-body <exact-UTF8-text>, and a summary log; it probes /config, logging POST, repeat POST 409, the normal-case binary vector response and final health 200 before terminating its QEMU and checking the stopped endpoint refuses connections.");
        Console.WriteLine("--service-quota-failure is an explicit expected capture under --continuing-service; requires replaced default markers and exact final CLROS_M16_016_QUOTA_CLEANUP, disables positive raw socket controls, retains frame/process/endpoint cleanup, and returns22 with acceptance pending. No quota or guest behavior is accepted by capture alone.");
        Console.WriteLine("--continuing-service requires the captured modern ARP fixture, guest port 8080 forward, virtio RNG, SingleMinimalApi, summary log and a 780..1080-second timeout (quota:780..900); owned raw socket controls run alongside serial capture until TEST_PASS m16-012 and confirmed process/frame cleanup. Other web, scripted peer, HTTP or panic modes are forbidden.");
        Console.WriteLine("--web-log-case normal|records64 requires --continuing-web (default normal); normal expects logging POST 200 and GET /__clros/vectors with 49152 exact binary bytes, records64 expects logging 500 with an empty body and skips vectors. Repeat POST 409 and final health 200 are required in both cases.");
        Console.WriteLine("--web-race-pipe <clros-web-race-32lowerhex> requires normal continuing-web and an owned local producer. After ordinary controls it waits for joined race-report hashes within the original deadline; dry-run/preflight opens no pipe.");
        Console.WriteLine("--startup-timeout <seconds> bounds startup to M03_EBS_RETURNED_SUCCESS when required (default 30); the overall --timeout still applies. A startup timeout captures QMP registers and up to 512 MiB RAM beside the serial log before terminating the child.");
        Console.WriteLine("OS.QemuRunner [preflight|--preflight] --image <raw|qcow2 image> [--disk-format raw|qcow2] [--workload-profile SingleMinimalApi|MultiMinimalApi] [--ovmf-code <OVMF_CODE.fd>] [--serial-log <path>] [--summary-log <path>] [--timeout <seconds>] [--replace-default-required-markers] [--require-marker <marker>]... [--require-ordered-marker <marker>]... [--require-unique-marker <marker>]... [--marker-preset " + SmokeMarkerPreset.SupportedValues + "]... [--forbid-marker <marker>]... [--http-probe-url <http-url>] [--http-probe-expected-status <100-599>] [--http-probe-expected-body <text>] [--http-probe-timeout <seconds>] [--expect-unexpected-vector-panic|--expect-owned-subsystem-panic] [--accelerator whpx|tcg] [--firmware-rng-source virtio|missing] [--virtio-block-queue-device [--virtio-block-boot-image <raw>]] [--dry-run]");
        Console.WriteLine("preflight validates local runner inputs and image format hints without launching QEMU or requiring libvirt.");
    }

    private void ReplaceDefaultRequiredMarkers()
    {
        if (defaultRequiredMarkersReplaced)
        {
            throw new ArgumentException("--replace-default-required-markers may be specified only once.");
        }

        if (markerConfigurationStarted)
        {
            throw new ArgumentException("--replace-default-required-markers must precede marker arguments.");
        }

        requiredMarkers.Clear();
        defaultRequiredMarkersReplaced = true;
    }

    private void AddRequiredMarker(string marker)
    {
        markerConfigurationStarted = true;
        AddDistinct(requiredMarkers, marker);
    }

    private void AddOrderedMarker(string marker)
    {
        AddDistinct(orderedMarkers, marker);
        AddRequiredMarker(marker);
    }

    private void AddUniqueMarker(string marker)
    {
        AddDistinct(uniqueMarkers, marker);
        AddRequiredMarker(marker);
    }

    private void AddMarkerPreset(string preset)
    {
        string normalized = SmokeMarkerPreset.Normalize(preset);
        AddDistinct(markerPresets, normalized);
        foreach (string marker in SmokeMarkerPreset.GetRequiredMarkers(normalized))
        {
            AddRequiredMarker(marker);
        }

        foreach (string marker in SmokeMarkerPreset.GetOrderedMarkers(normalized))
        {
            AddOrderedMarker(marker);
        }
    }

    private void AddForbiddenMarker(string marker)
    {
        markerConfigurationStarted = true;
        AddDistinct(forbiddenMarkers, marker);
    }

    private void SetDiskFormat(string value)
    {
        if (string.Equals(value, "raw", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "raw";
            return;
        }

        if (string.Equals(value, "qcow2", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "qcow2";
            return;
        }

        throw new ArgumentException("Value for --disk-format must be raw or qcow2.");
    }

    private void SetHttpProbeUrl(string value)
    {
        httpProbeConfigured = true;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for --http-probe-url cannot be empty.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Value for --http-probe-url must be an absolute HTTP URL with a host.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HTTP probe URL must use http scheme; https/TLS is not supported.");
        }

        httpProbeUrl = value;
    }

    private void SetHttpProbeExpectedStatusCode(int statusCode)
    {
        httpProbeConfigured = true;
        httpProbeExpectedStatusCode = statusCode;
    }

    private void SetHttpProbeExpectedBody(string body)
    {
        httpProbeConfigured = true;
        httpProbeExpectedBody = body;
        httpProbeExpectedBodyConfigured = true;
    }

    private void SetHttpProbeTimeoutSeconds(int timeoutSeconds)
    {
        httpProbeConfigured = true;
        httpProbeTimeoutSeconds = timeoutSeconds;
    }

    private void FinishParsing()
    {
        if (ServiceQuotaFailure)
        {
            if (!ContinuingService || !defaultRequiredMarkersReplaced || MarkerPresets.Count != 0)
                throw new ArgumentException("--service-quota-failure requires --continuing-service and --replace-default-required-markers without marker presets.");
            foreach (string marker in ServiceQuotaCapture.ForbiddenSuccessMarkers)
            {
                if (RequiredMarkers.Concat(OrderedMarkers).Concat(UniqueMarkers).Any(required => required == marker && marker != ServiceQuotaCapture.EarlyBootSmokePass || required.StartsWith(marker + " ", StringComparison.Ordinal)))
                    throw new ArgumentException("Quota capture forbids ordinary success requirements.");
                // The inherited boot pass is phase-checked by ServiceQuotaCapture.
                if (marker != ServiceQuotaCapture.EarlyBootSmokePass)
                    AddForbiddenMarker(marker);
            }

            AddRequiredMarker(ServiceQuotaCapture.Terminal);
            AddUniqueMarker(ServiceQuotaCapture.Terminal);
        }
        else if (ContinuingService)
            AddRequiredMarker("TEST_PASS m16-012");
        if (FiniteStaticFiles)
        {
            StaticEvidence = new();
            AddRequiredMarker(DiskStaticFiles ? "CLROS_M10_018_READY" : "CLROS_M10_017_READY");
            AddUniqueMarker(DiskStaticFiles ? "CLROS_M10_018_READY" : "CLROS_M10_017_READY");
            AddRequiredMarker(DiskStaticFiles ? "TEST_PASS m10.018" : "TEST_PASS m10.017");
            AddUniqueMarker(DiskStaticFiles ? "TEST_PASS m10.018" : "TEST_PASS m10.017");
            AddRequiredMarker("TEST_PASS m16-012");
        }

        if (ContinuingWeb)
        {
            AddRequiredMarker("CLROS_M10_004_READY");
            AddRequiredMarker("TEST_PASS m10.004");
        }

        if (DryRun && Preflight)
        {
            throw new ArgumentException("--dry-run and preflight are mutually exclusive.");
        }

        if (new[]
        {
            ExpectedUnexpectedVectorPanic,
            ExpectedOwnedSubsystemPanic,
            ExpectedUnhandledException
        }.Count(x => x) > 1)
            throw new ArgumentException("Expected-panic profiles are mutually exclusive.");
        if ((ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException) && (httpProbeConfigured || string.IsNullOrWhiteSpace(SummaryLogPath) || MarkerPresets.Count != 0))
            throw new ArgumentException("Expected unexpected-vector panic requires a summary log and forbids HTTP probing or marker presets.");
        ValidateArtifactPathSeparation();
        // Constructing the parser validates marker counts, lengths, ASCII,
        // duplicates, and required/forbidden overlap before any process can
        // be launched.
        _ = new SerialMarkerStateMachine(RequiredMarkers, ForbiddenMarkers, successTerminalMarker: SuccessTerminalMarker, byteCapacity: SerialByteCapacity, orderedMarkers: OrderedMarkers, uniqueMarkers: UniqueMarkers);
        if (!httpProbeConfigured)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(httpProbeUrl))
        {
            throw new ArgumentException("HTTP probe requires --http-probe-url.");
        }

        HttpProbe = new HttpProbeOptions(httpProbeUrl, httpProbeExpectedStatusCode, httpProbeExpectedBody, httpProbeExpectedBodyConfigured, httpProbeTimeoutSeconds);
    }

    private void ValidateContinuingService()
    {
        if (!ContinuingService)
            return;
        if (!VirtioNegotiationDevice || !ArpReceiveFixture || EthernetCapturePath is null || SocketForwardPort is null || string.IsNullOrWhiteSpace(SummaryLogPath) || WorkloadProfile != OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName || FirmwareRngSource != "virtio" || TimeoutSeconds < 780 || TimeoutSeconds > (ServiceQuotaFailure ? 900 : 1080) || ContinuingWeb || ContinuingNetworkPeer || SocketContainmentPeer || httpProbeConfigured || ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException)
            throw new ArgumentException("--continuing-service requires captured modern ARP networking, guest port 8080 forward, summary log, SingleMinimalApi, virtio RNG and a 780..1080-second timeout (quota:780..900) without web, peer, HTTP or panic modes.");
        ServiceEvidence = new()
        {
            QuotaFailure = ServiceQuotaFailure
        };
    }

    private void ValidateContinuingWeb()
    {
        if (!ContinuingWeb)
        {
            if (WebEventOwnedSignalRace)
                throw new ArgumentException("--web-event-owned-signal-race requires --continuing-web.");
            if (WebRacePipe is not null)
                throw new ArgumentException("--web-race-pipe requires --continuing-web.");
            if (webLogCaseConfigured)
                throw new ArgumentException("--web-log-case requires --continuing-web.");
            if (webConfigBody is not null)
                throw new ArgumentException("--web-config-body requires --continuing-web.");
            return;
        }

        if (WebEventOwnedSignalRace && WebRacePipe is null)
            throw new ArgumentException("--web-event-owned-signal-race requires --web-race-pipe.");
        if (WebRacePipe is not null)
        {
            if (WebEventOwnedSignalRace)
                WebRaceRendezvous.ValidateEventOwnedPipeName(WebRacePipe);
            else
                WebRaceRendezvous.ValidatePipeName(WebRacePipe);
        }

        if (WebRacePipe is not null && WebLogCase != "normal")
            throw new ArgumentException("--web-race-pipe requires normal web logging.");
        if (SocketForwardPort is null || HttpProbe is null || string.IsNullOrWhiteSpace(SummaryLogPath) || WorkloadProfile != OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName || TimeoutSeconds > 285 || ContinuingNetworkPeer || ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException)
            throw new ArgumentException("--continuing-web requires a captured guest 8080 forward, HTTP probe, summary log, SingleMinimalApi and timeout at most 285 seconds without a scripted peer or panic profile.");
        HttpProbeOptions probe = HttpProbe.Value;
        Uri uri = new(probe.Url, UriKind.Absolute);
        if (uri.Host != "127.0.0.1" || uri.Port != SocketForwardPort || uri.PathAndQuery != "/healthz" || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || probe.ExpectedStatusCode != 200 || !probe.ExpectedBodyConfigured || probe.ExpectedBodyByteLength is < 1 or > 65536 || probe.TimeoutSeconds > 30 || webConfigBody is null || Encoding.UTF8.GetByteCount(webConfigBody)is < 1 or > 65536)
            throw new ArgumentException("--continuing-web requires exact http://127.0.0.1:<forward>/healthz, status 200, bounded nonempty health/config bodies and an HTTP timeout at most 30 seconds.");
        // The inherited boot prefix can require OS_SMOKE_PASS. Web startup
        // still requires both M10 markers and uses its own success terminal.
        WebConfigProbe = new HttpProbeOptions("http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/config", 200, webConfigBody, true, probe.TimeoutSeconds);
        string loggingUrl = "http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/__clros/logexercise";
        bool quota = WebLogCase == "records64";
        WebLogProbe = new(loggingUrl, quota ? 500 : 200, // Eight traced writers need a larger finite request budget than health.
        // ExecuteContinuingWebProbe still clamps it to the owned guest deadline.
        quota ? "" : "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9", true, 60, post: true);
        WebLogRepeatProbe = new(loggingUrl, 409, "", true, probe.TimeoutSeconds, post: true);
        if (!quota)
            WebVectorProbe = new("http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/__clros/vectors", 200, "", true, probe.TimeoutSeconds, vectorBody: true);
        WebEvidence = new ContinuingWebEvidence();
    }

    private void ValidateArtifactPathSeparation()
    {
        List<(string Name, string Path)> paths = [("image", ImagePath), ("serial-log", SerialLogPath), ("qemu-requested", QemuPath)];
        if (VirtioBlockBootImagePath is not null)
            paths.Add(("virtio-block-boot-image", VirtioBlockBootImagePath));
        if (WritableDataImagePath is not null)
        {
            paths.Add(("writable-data-image", WritableDataImagePath));
            if (EthernetCapturePath is not null)
                paths.Add(("ethernet-capture", EthernetCapturePath));
        }

        if (!string.IsNullOrWhiteSpace(OvmfCodePath))
        {
            paths.Add(("ovmf-code", OvmfCodePath));
        }

        if (!string.IsNullOrWhiteSpace(SummaryLogPath))
        {
            paths.Add(("summary-log", SummaryLogPath));
        }

        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, string> nameByPath = new Dictionary<string, string>(comparer);
        foreach ((string name, string path)in paths)
        {
            string fullPath = Path.GetFullPath(path);
            if (nameByPath.TryGetValue(fullPath, out string? existing))
            {
                throw new ArgumentException("Artifact paths must be distinct: " + existing + " and " + name + " resolve to " + fullPath + ".");
            }

            nameByPath.Add(fullPath, name);
        }
    }

    private static string NormalizeWorkloadProfile(string value)
    {
        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
        }

        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName;
        }

        throw new ArgumentException("Invalid workload profile '" + value + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
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

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
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

    private static int ReadHttpStatusCode(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed < 100 || parsed > 599)
        {
            throw new ArgumentException("Value for " + option + " must be an HTTP status code from 100 to 599.");
        }

        return parsed;
    }
}
