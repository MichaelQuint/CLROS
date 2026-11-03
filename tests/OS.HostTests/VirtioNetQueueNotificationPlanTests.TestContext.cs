using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetQueueNotificationPlanTests
{
    private readonly struct TestContext
    {
        public TestContext(VirtioPciBarApertureSet apertures, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            QueueSetup = queueSetup;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }
        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }
        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
