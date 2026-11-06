using System.Text;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Budget = OS.Kernel.Compatibility.LinuxX64.LinuxAppLogBudget;

internal static unsafe partial class LinuxAppLoggingTests
{
    // Descriptor callbacks retain their existing model at offset0. Only the
    // output callback is replaced; real page validation/copy/dispatch still runs.
    private struct LoggingModel
    {
        public LinuxDescriptorServicesTests.Model Descriptor;
        public Budget Budget;
        public Budget.ChargeStatus LastCharge;
        public uint DeliveredRecords;
        public bool FailDelivery, QuotaCase, ObservationFailed;
        public ulong Writer;
        public LinuxAppLogQuotaObservation Quota;
    }
}
