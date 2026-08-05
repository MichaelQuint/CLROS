namespace OS.Boot;

public enum M02Com1OwnedPortIoBackendIntegrationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    CheckedBindingNotReady = 2,
    BackendNotReady = 3,
    ConfigurationRejected = 4,
    ConfigurationWriteCountMismatch = 5
}

public readonly struct M02Com1OwnedPortIoBackendIntegrationResult
{
    private readonly byte _isInitialized;
    private readonly byte _guestPortIo;
    private readonly byte _guestIn8;
    private readonly byte _markerTransportUsesUefiSerial;

    private M02Com1OwnedPortIoBackendIntegrationResult(
        M02Com1OwnedPortIoBackendIntegrationStatus status,
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
            bindingStatus,
        OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult configuration,
        bool guestPortIo)
    {
        _isInitialized = 1;
        _guestPortIo = guestPortIo ? (byte)1 : (byte)0;
        _guestIn8 = 0;
        _markerTransportUsesUefiSerial = 1;
        Status = status;
        BindingStatus = bindingStatus;
        Configuration = configuration;
    }

    public bool IsInitialized =>
        _isInitialized == 1 &&
        _markerTransportUsesUefiSerial == 1;

    public M02Com1OwnedPortIoBackendIntegrationStatus Status { get; }

    public OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
        BindingStatus { get; }

    public OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult
        Configuration { get; }

    public int ConfigurationWrites => Configuration.WritesCompleted;

    public bool GuestPortIo => _guestPortIo != 0;

    public bool GuestIn8 => _guestIn8 != 0;

    public bool MarkerTransportUsesUefiSerial =>
        _markerTransportUsesUefiSerial != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == M02Com1OwnedPortIoBackendIntegrationStatus.Success &&
        BindingStatus ==
            OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus.Ready &&
        Configuration.Succeeded &&
        ConfigurationWrites ==
            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                .RequiredConfigurationWriteCount &&
        GuestPortIo &&
        !GuestIn8 &&
        MarkerTransportUsesUefiSerial;

    internal static M02Com1OwnedPortIoBackendIntegrationResult Pass(
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
            bindingStatus,
        OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult configuration)
    {
        return new M02Com1OwnedPortIoBackendIntegrationResult(
            M02Com1OwnedPortIoBackendIntegrationStatus.Success,
            bindingStatus,
            configuration,
            guestPortIo: true);
    }

    internal static M02Com1OwnedPortIoBackendIntegrationResult Fail(
        M02Com1OwnedPortIoBackendIntegrationStatus status,
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
            bindingStatus,
        OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult configuration =
            default)
    {
        return new M02Com1OwnedPortIoBackendIntegrationResult(
            status,
            bindingStatus,
            configuration,
            guestPortIo: false);
    }
}

public static class M02Com1OwnedPortIoBackendIntegration
{
    public const int ExactConfigurationWriteCount =
        OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
            .RequiredConfigurationWriteCount;
    public const bool GuestIn8 = false;
    public const bool MarkerTransportUsesUefiSerial = true;
    public const bool PostExitBootServices = false;

    public static M02Com1OwnedPortIoBackendIntegrationResult Configure(
        M02CheckedPortIoFunctionPointerIntegrationResult checkedBinding)
    {
        if (!checkedBinding.Succeeded)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(
                M02Com1OwnedPortIoBackendIntegrationStatus
                    .CheckedBindingNotReady,
                checkedBinding.BindingStatus);
        }

        OS.Kernel.Diagnostics.Com1OwnedPortIoBackend backend =
            new(checkedBinding.Binding);
        if (!backend.IsReady)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(
                M02Com1OwnedPortIoBackendIntegrationStatus.BackendNotReady,
                backend.BindingStatus);
        }

        OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult configuration =
            backend.ConfigureDefault();
        if (!configuration.Succeeded)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(
                M02Com1OwnedPortIoBackendIntegrationStatus
                    .ConfigurationRejected,
                backend.BindingStatus,
                configuration);
        }

        if (configuration.WritesCompleted != ExactConfigurationWriteCount)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(
                M02Com1OwnedPortIoBackendIntegrationStatus
                    .ConfigurationWriteCountMismatch,
                backend.BindingStatus,
                configuration);
        }

        return M02Com1OwnedPortIoBackendIntegrationResult.Pass(
            backend.BindingStatus,
            configuration);
    }
}
