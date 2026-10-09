using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class M05PhysicalMappingEfiIntegrationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        const string marker = "m05.001.efi disabled boot gate";
        Console.WriteLine("TEST_START " + marker);
        int invocations = 0;
        if (Probes.EnableM05PhysicalMappingProbe ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(false, &invocations, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, null, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, null) ||
            invocations != 0)
        {
            Console.WriteLine("TEST_FAIL " + marker + " disabled profile invoked callback");
            throw new InvalidOperationException("disabled mapping profile");
        }
        Console.WriteLine("TEST_PASS " + marker);
    }

    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }
}
