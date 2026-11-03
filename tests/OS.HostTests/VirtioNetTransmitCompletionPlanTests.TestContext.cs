using System.Runtime.CompilerServices;
using System.Reflection;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitCompletionPlanTests
{
    private readonly struct TestContext
    {
        public TestContext(VirtioPciBarApertureSet apertures, VirtioPciBarRegisterWindowBindingResult registerWindows, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            RegisterWindows = registerWindows;
            QueueSetup = queueSetup;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }
        public VirtioPciBarRegisterWindowBindingResult RegisterWindows { get; }
        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }
        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
