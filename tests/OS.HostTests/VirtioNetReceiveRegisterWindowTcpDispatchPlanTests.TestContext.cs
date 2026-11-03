using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetReceiveRegisterWindowTcpDispatchPlanTests
{
    private readonly struct TestContext
    {
        public TestContext(VirtioPciBarApertureSet apertures, VirtioPciBarRegisterWindowBindingResult registerWindows, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupCommand[] queueSetupCommands, VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            RegisterWindows = registerWindows;
            QueueSetup = queueSetup;
            QueueSetupCommands = queueSetupCommands;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }
        public VirtioPciBarRegisterWindowBindingResult RegisterWindows { get; }
        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }
        public VirtioNetQueueMmioSetupCommand[] QueueSetupCommands { get; }
        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
