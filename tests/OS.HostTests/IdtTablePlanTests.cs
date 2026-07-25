using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class IdtTablePlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("idt table plan binds required fault vectors", TestBindsRequiredFaultVectors, ref failures);
        Run("idt table plan rejects missing required vector", TestRejectsMissingRequiredVector, ref failures);
        Run("idt table plan requires double fault ist", TestRequiresDoubleFaultIst, ref failures);
        Run("idt table plan writes descriptor bytes", TestWritesDescriptorBytes, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("IDT table plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestBindsRequiredFaultVectors()
    {
        IdtGateDescriptor[] table = new IdtGateDescriptor[IdtTablePlan.EntryCount];
        ulong[] handlers = CreateHandlerTable();

        IdtTablePlanResult createResult = IdtTablePlan.CreateRequiredFaultTable(
            handlers,
            codeSelector: 0x0008,
            defaultIstIndex: 0,
            doubleFaultIstIndex: 1,
            table);

        IdtTablePlanResult validateResult = IdtTablePlan.ValidateRequiredFaultTable(table);

        return createResult.Succeeded
            && createResult.Status == IdtTablePlanStatus.Ok
            && validateResult.Succeeded
            && validateResult.Status == IdtTablePlanStatus.Ok
            && IdtTablePlan.RequiredFaultVectorCount == 9
            && table[FaultVectorCatalog.DivideError].HandlerAddress == handlers[0]
            && table[FaultVectorCatalog.InvalidOpcode].HandlerAddress == handlers[1]
            && table[FaultVectorCatalog.DoubleFault].IstIndex == 1
            && table[FaultVectorCatalog.PageFault].Present
            && IdtTablePlan.IsRequiredFaultVector(FaultVectorCatalog.GeneralProtectionFault)
            && !IdtTablePlan.IsRequiredFaultVector(FaultVectorCatalog.Breakpoint);
    }

    private static bool TestRejectsMissingRequiredVector()
    {
        IdtGateDescriptor[] table = CreateValidTable();
        table[FaultVectorCatalog.PageFault] = default;

        IdtTablePlanResult missingPageFault = IdtTablePlan.ValidateRequiredFaultTable(table);

        table = CreateValidTable();
        table[FaultVectorCatalog.DoubleFault] = default;

        IdtTablePlanResult missingDoubleFault = IdtTablePlan.ValidateRequiredFaultTable(table);

        return !missingPageFault.Succeeded
            && missingPageFault.Status == IdtTablePlanStatus.MissingRequiredVector
            && missingPageFault.Vector == FaultVectorCatalog.PageFault
            && !missingDoubleFault.Succeeded
            && missingDoubleFault.Status == IdtTablePlanStatus.MissingRequiredVector
            && missingDoubleFault.Vector == FaultVectorCatalog.DoubleFault;
    }

    private static bool TestRequiresDoubleFaultIst()
    {
        IdtGateDescriptor[] table = new IdtGateDescriptor[IdtTablePlan.EntryCount];

        IdtTablePlanResult createResult = IdtTablePlan.CreateRequiredFaultTable(
            CreateHandlerTable(),
            codeSelector: 0x0008,
            defaultIstIndex: 0,
            doubleFaultIstIndex: 0,
            table);

        IdtGateDescriptor[] invalidTable = CreateValidTable();
        IdtGateDescriptorResult noIstDoubleFault = IdtGateDescriptor.Create(
            FaultVectorCatalog.DoubleFault,
            handlerAddress: 0xffff800000008000,
            codeSelector: 0x0008,
            istIndex: 0,
            gateType: IdtGateType.InterruptGate,
            descriptorPrivilegeLevel: 0,
            present: true);
        invalidTable[FaultVectorCatalog.DoubleFault] = noIstDoubleFault.Descriptor;

        IdtTablePlanResult validateResult = IdtTablePlan.ValidateRequiredFaultTable(invalidTable);

        return !createResult.Succeeded
            && createResult.Status == IdtTablePlanStatus.DoubleFaultIstRequired
            && createResult.Vector == FaultVectorCatalog.DoubleFault
            && noIstDoubleFault.Succeeded
            && !validateResult.Succeeded
            && validateResult.Status == IdtTablePlanStatus.DoubleFaultIstRequired
            && validateResult.Vector == FaultVectorCatalog.DoubleFault;
    }

    private static bool TestWritesDescriptorBytes()
    {
        IdtGateDescriptor[] table = CreateValidTable();
        byte[] bytes = new byte[IdtTablePlan.EncodedByteLength];

        IdtTablePlanResult writeResult = IdtTablePlan.WriteDescriptorBytes(table, bytes);
        int pageFaultOffset = FaultVectorCatalog.PageFault * IdtGateDescriptor.EncodedLength;
        int doubleFaultOffset = FaultVectorCatalog.DoubleFault * IdtGateDescriptor.EncodedLength;

        IdtTablePlanResult smallBufferResult = IdtTablePlan.WriteDescriptorBytes(
            table,
            new byte[IdtTablePlan.EncodedByteLength - 1]);

        return writeResult.Succeeded
            && writeResult.RequiredBytes == IdtTablePlan.EncodedByteLength
            && bytes.Length == 4096
            && bytes[pageFaultOffset + 0] == 0x00
            && bytes[pageFaultOffset + 1] == 0x90
            && bytes[pageFaultOffset + 2] == 0x08
            && bytes[pageFaultOffset + 3] == 0x00
            && bytes[pageFaultOffset + 5] == 0x8e
            && bytes[doubleFaultOffset + 4] == 0x01
            && bytes[doubleFaultOffset + 5] == 0x8e
            && !smallBufferResult.Succeeded
            && smallBufferResult.Status == IdtTablePlanStatus.DescriptorBufferTooSmall;
    }

    private static IdtGateDescriptor[] CreateValidTable()
    {
        IdtGateDescriptor[] table = new IdtGateDescriptor[IdtTablePlan.EntryCount];
        IdtTablePlanResult result = IdtTablePlan.CreateRequiredFaultTable(
            CreateHandlerTable(),
            codeSelector: 0x0008,
            defaultIstIndex: 0,
            doubleFaultIstIndex: 1,
            table);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Expected valid IDT table plan.");
        }

        return table;
    }

    private static ulong[] CreateHandlerTable()
    {
        ulong[] handlers = new ulong[IdtTablePlan.RequiredFaultVectorCount];
        for (int index = 0; index < handlers.Length; index++)
        {
            handlers[index] = 0xffff800000001000UL + ((ulong)index * 0x1000UL);
        }

        return handlers;
    }
}
