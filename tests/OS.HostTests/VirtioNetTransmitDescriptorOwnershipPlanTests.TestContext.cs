using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitDescriptorOwnershipPlanTests
{
    private readonly struct TestContext
    {
        public TestContext(VirtioPciBarApertureSet apertures, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupCommand[] queueSetupCommands, VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            QueueSetup = queueSetup;
            QueueSetupCommands = queueSetupCommands;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures
        {
            get;
        }
        public VirtioNetQueueMmioSetupPlanResult QueueSetup
        {
            get;
        }
        public VirtioNetQueueMmioSetupCommand[] QueueSetupCommands
        {
            get;
        }
        public VirtioNetQueuePlanResult QueuePlan
        {
            get;
        }
    }
}
