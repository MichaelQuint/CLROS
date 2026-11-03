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
