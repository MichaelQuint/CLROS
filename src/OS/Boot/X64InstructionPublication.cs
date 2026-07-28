namespace OS.Boot
{
    internal static class X64InstructionPublication
    {
        internal const uint ExpectedSuccessCookie = 0x53524C43u;

        [System.Runtime.RuntimeImport(
            "*",
            "CLROS_X64_SerializeInstructionPublication")]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern uint Serialize();
    }
}
