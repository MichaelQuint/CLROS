namespace OS.Tools.QemuRunner;

public static class SmokeMarkerPreset
{
    public const string Baseline = "baseline";
    public const string Acpi = "acpi";
    public const string AppArgs = "app-args";
    public const string AppExit = "app-exit";
    public const string AppLifecycle = "app-lifecycle";
    public const string AppSelection = "app-selection";
    public const string CctorPreflight = "cctor-preflight";
    public const string CctorWorkItems = "cctor-work-items";
    public const string Diagnostics = "diagnostics";
    public const string FetchHttp = "fetch-http";
    public const string FetchHttpEndpoint = "fetch-http-endpoint";
    public const string FetchHttpTcpExchange = "fetch-http-tcp-exchange";
    public const string HttpStaticResponse = "http-static-response";
    public const string HttpStaticTcpResponse = "http-static-tcp-response";
    public const string ManifestEnv = "manifest-env";
    public const string NativeAotHeap = "nativeaot-heap";
    public const string PalExport = "pal-export";
    public const string UefiGone = "uefi-gone";
    public const string VirtioRxTcpDispatch = "virtio-rx-tcp-dispatch";

    private static readonly string[] BaselineRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS"
    ];

    private static readonly string[] AcpiRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "ACPI_RSDP_SNAPSHOT_READY",
        "ACPI_RSDP_VALIDATED",
        "ACPI_ROOT_SDT_SNAPSHOT_READY",
        "ACPI_ROOT_SDT_VALIDATED",
        "ACPI_MCFG_DISCOVERY_READY"
    ];

    private static readonly string[] AppExitRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "APP_EXIT_OBSERVER_READY"
    ];

    private static readonly string[] AppLifecycleRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "APP_LIFECYCLE_READY"
    ];

    private static readonly string[] AppArgsRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "APP_ARGS_VECTOR_READY"
    ];

    private static readonly string[] AppSelectionRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "APP_SELECTION_READY"
    ];

    private static readonly string[] CctorPreflightRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY"
    ];

    private static readonly string[] CctorWorkItemsRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "NATIVEAOT_CCTOR_WORK_ITEMS_READY"
    ];

    private static readonly string[] UefiGoneRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "UEFI_GONE_SIM_START",
        "UEFI_GONE_SIM_PLATFORM_DENIED",
        "POST_FIRMWARE_DIAGNOSTICS_READY",
        "UEFI_GONE_SIM_PASS"
    ];

    private static readonly string[] FetchHttpRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "FETCH_APP_HTTP_COMPLETION_READY"
    ];

    private static readonly string[] FetchHttpEndpointRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "FETCHAPP_HTTP_ENDPOINT_READY"
    ];

    private static readonly string[] FetchHttpTcpExchangeRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "FETCH_APP_HTTP_TCP_EXCHANGE_READY"
    ];

    private static readonly string[] HttpStaticResponseRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "HTTP_STATIC_RESPONSE_READY",
        "HTTP_STATIC_RESPONSE"
    ];

    private static readonly string[] HttpStaticTcpResponseRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "HTTP_STATIC_TCP_RESPONSE_READY",
        "HTTP_STATIC_TCP_RESPONSE"
    ];

    private static readonly string[] VirtioRxTcpDispatchRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "VIRTIO_NET_RX_TCP_DISPATCH_READY",
        "VIRTIO_NET_RX_TCP_DISPATCH"
    ];

    private static readonly string[] ManifestEnvRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "NATIVE_MANIFEST_ENV_READY"
    ];

    private static readonly string[] NativeAotHeapRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "NATIVEAOT_HEAP_BOOTSTRAP_READY"
    ];

    private static readonly string[] PalExportRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "PAL_EXPORT_CATALOG_READY"
    ];

    private static readonly string[] DiagnosticsRequiredMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "FAULT_INFRA_READY",
        "EARLY_DIAGNOSTICS_READY",
        "DIAGNOSTIC_STUBS_MATERIALIZED",
        "CONTROLLED_FAULT_PROBES_READY",
        "CPU_FAULT_PANIC_READY",
        "DIAGNOSTICS_CHAIN_READY",
        "DIAGNOSTIC_STUB_INVOCATION_READY"
    ];

    private static readonly string[] AcpiOrderedMarkers =
    [
        "ACPI_RSDP_SNAPSHOT_READY",
        "ACPI_RSDP_VALIDATED",
        "ACPI_ROOT_SDT_SNAPSHOT_READY",
        "ACPI_ROOT_SDT_VALIDATED",
        "ACPI_MCFG_DISCOVERY_READY"
    ];

    private static readonly string[] DiagnosticsOrderedMarkers =
    [
        "FAULT_INFRA_READY",
        "EARLY_DIAGNOSTICS_READY",
        "DIAGNOSTIC_STUBS_MATERIALIZED",
        "CONTROLLED_FAULT_PROBES_READY",
        "CPU_FAULT_PANIC_READY",
        "DIAGNOSTICS_CHAIN_READY",
        "DIAGNOSTIC_STUB_INVOCATION_READY"
    ];

    public static string SupportedValues =>
        Baseline + "," + Acpi + "," + AppArgs + "," + AppExit + "," + AppLifecycle + "," + AppSelection + "," + CctorPreflight + "," + CctorWorkItems + "," + Diagnostics + "," + FetchHttp + "," + FetchHttpEndpoint + "," + FetchHttpTcpExchange + "," + HttpStaticResponse + "," + HttpStaticTcpResponse + "," + ManifestEnv + "," + NativeAotHeap + "," + PalExport + "," + UefiGone + "," + VirtioRxTcpDispatch;

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Marker preset cannot be empty.");
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "default" => Baseline,
            Baseline => Baseline,
            Acpi => Acpi,
            "app_args" => AppArgs,
            "app-arguments" => AppArgs,
            "native-app-args" => AppArgs,
            AppArgs => AppArgs,
            "appexit" => AppExit,
            "app_exit" => AppExit,
            AppExit => AppExit,
            "app_lifecycle" => AppLifecycle,
            "native-app-lifecycle" => AppLifecycle,
            "applifecycle" => AppLifecycle,
            AppLifecycle => AppLifecycle,
            "app_selection" => AppSelection,
            "native-app-selection" => AppSelection,
            "appselection" => AppSelection,
            AppSelection => AppSelection,
            "cctor" => CctorPreflight,
            "cctor_preflight" => CctorPreflight,
            CctorPreflight => CctorPreflight,
            "cctor_work_items" => CctorWorkItems,
            "cctor-workitem" => CctorWorkItems,
            "cctor-work-item" => CctorWorkItems,
            CctorWorkItems => CctorWorkItems,
            Diagnostics => Diagnostics,
            "fetchhttp" => FetchHttp,
            "fetch_http" => FetchHttp,
            "fetchapp-http" => FetchHttp,
            "fetchapp_http" => FetchHttp,
            FetchHttp => FetchHttp,
            "fetch_endpoint" => FetchHttpEndpoint,
            "fetchhttp-endpoint" => FetchHttpEndpoint,
            "fetchapp-http-endpoint" => FetchHttpEndpoint,
            FetchHttpEndpoint => FetchHttpEndpoint,
            "fetch_http_tcp_exchange" => FetchHttpTcpExchange,
            "fetch-http-exchange" => FetchHttpTcpExchange,
            "fetchapp-http-tcp-exchange" => FetchHttpTcpExchange,
            FetchHttpTcpExchange => FetchHttpTcpExchange,
            "http_static_response" => HttpStaticResponse,
            "tiny-http-static" => HttpStaticResponse,
            "tinyhttp-static" => HttpStaticResponse,
            HttpStaticResponse => HttpStaticResponse,
            "http_static_tcp_response" => HttpStaticTcpResponse,
            "tiny-http-tcp-static" => HttpStaticTcpResponse,
            "tinyhttp-tcp-static" => HttpStaticTcpResponse,
            HttpStaticTcpResponse => HttpStaticTcpResponse,
            "virtio_rx_tcp_dispatch" => VirtioRxTcpDispatch,
            "virtio-net-rx-tcp-dispatch" => VirtioRxTcpDispatch,
            "virtionet-rx-tcp-dispatch" => VirtioRxTcpDispatch,
            VirtioRxTcpDispatch => VirtioRxTcpDispatch,
            "native-manifest-env" => ManifestEnv,
            "manifest_environment" => ManifestEnv,
            "manifestenv" => ManifestEnv,
            ManifestEnv => ManifestEnv,
            "nativeaot_heap" => NativeAotHeap,
            "heap-bootstrap" => NativeAotHeap,
            "nativeaot-heap-bootstrap" => NativeAotHeap,
            NativeAotHeap => NativeAotHeap,
            "pal_export" => PalExport,
            "pal-catalog" => PalExport,
            "pal-export-catalog" => PalExport,
            PalExport => PalExport,
            "uefigone" => UefiGone,
            "uefi_gone" => UefiGone,
            UefiGone => UefiGone,
            _ => throw new ArgumentException("Unknown marker preset '" + value + "'. Valid values: " + SupportedValues + ".")
        };
    }

    public static IReadOnlyList<string> GetRequiredMarkers(string normalizedPreset)
    {
        return normalizedPreset switch
        {
            Baseline => BaselineRequiredMarkers,
            Acpi => AcpiRequiredMarkers,
            AppArgs => AppArgsRequiredMarkers,
            AppExit => AppExitRequiredMarkers,
            AppLifecycle => AppLifecycleRequiredMarkers,
            AppSelection => AppSelectionRequiredMarkers,
            CctorPreflight => CctorPreflightRequiredMarkers,
            CctorWorkItems => CctorWorkItemsRequiredMarkers,
            Diagnostics => DiagnosticsRequiredMarkers,
            FetchHttp => FetchHttpRequiredMarkers,
            FetchHttpEndpoint => FetchHttpEndpointRequiredMarkers,
            FetchHttpTcpExchange => FetchHttpTcpExchangeRequiredMarkers,
            HttpStaticResponse => HttpStaticResponseRequiredMarkers,
            HttpStaticTcpResponse => HttpStaticTcpResponseRequiredMarkers,
            VirtioRxTcpDispatch => VirtioRxTcpDispatchRequiredMarkers,
            ManifestEnv => ManifestEnvRequiredMarkers,
            NativeAotHeap => NativeAotHeapRequiredMarkers,
            PalExport => PalExportRequiredMarkers,
            UefiGone => UefiGoneRequiredMarkers,
            _ => throw new ArgumentException("Unknown marker preset '" + normalizedPreset + "'. Valid values: " + SupportedValues + ".")
        };
    }

    public static IReadOnlyList<string> GetOrderedMarkers(string normalizedPreset)
    {
        return normalizedPreset switch
        {
            Acpi => AcpiOrderedMarkers,
            Diagnostics => DiagnosticsOrderedMarkers,
            Baseline or AppArgs or AppExit or AppLifecycle or AppSelection or CctorPreflight or CctorWorkItems or FetchHttp or FetchHttpEndpoint or FetchHttpTcpExchange or HttpStaticResponse or HttpStaticTcpResponse or ManifestEnv or NativeAotHeap or PalExport or UefiGone or VirtioRxTcpDispatch => Array.Empty<string>(),
            _ => throw new ArgumentException("Unknown marker preset '" + normalizedPreset + "'. Valid values: " + SupportedValues + ".")
        };
    }
}
