namespace OS.Boot;

public static class M02Com1OwnedPortIoBackendIntegration
{
    public const int ExactConfigurationWriteCount = OS.Kernel.Diagnostics.Com1OwnedPortIoBackend.RequiredConfigurationWriteCount;
    public const bool GuestIn8 = false;
    public const bool MarkerTransportUsesUefiSerial = true;
    public const bool PostExitBootServices = false;
    public static M02Com1OwnedPortIoBackendIntegrationResult Configure(M02CheckedPortIoFunctionPointerIntegrationResult checkedBinding)
    {
        if (!checkedBinding.Succeeded)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(M02Com1OwnedPortIoBackendIntegrationStatus.CheckedBindingNotReady, checkedBinding.BindingStatus);
        }

        OS.Kernel.Diagnostics.Com1OwnedPortIoBackend backend = new(checkedBinding.Binding);
        if (!backend.IsReady)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(M02Com1OwnedPortIoBackendIntegrationStatus.BackendNotReady, backend.BindingStatus);
        }

        OS.Kernel.Diagnostics.Com1OwnedPortIoConfigurationResult configuration = backend.ConfigureDefault();
        if (!configuration.Succeeded)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(M02Com1OwnedPortIoBackendIntegrationStatus.ConfigurationRejected, backend.BindingStatus, configuration);
        }

        if (configuration.WritesCompleted != ExactConfigurationWriteCount)
        {
            return M02Com1OwnedPortIoBackendIntegrationResult.Fail(M02Com1OwnedPortIoBackendIntegrationStatus.ConfigurationWriteCountMismatch, backend.BindingStatus, configuration);
        }

        return M02Com1OwnedPortIoBackendIntegrationResult.Pass(backend.BindingStatus, configuration);
    }
}
