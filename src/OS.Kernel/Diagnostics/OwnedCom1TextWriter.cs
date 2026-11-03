namespace OS.Kernel.Diagnostics;

/// <summary>Writes the low byte of each text character through exclusively owned COM1.</summary>
/// <remarks>
/// The caller retains the immutable text and configured UART for this synchronous
/// call, on the owning CPU with interrupts disabled and no returning reentrant
/// writer. A terminal fault/NMI may abandon the incomplete record and halt;
/// ordinary output must never resume after that takeover. No storage is allocated.
/// A false result may follow an exact written prefix and invalidates the enclosing
/// evidence. Nothing is dropped or retried. Empty text succeeds without port I/O.
/// Only an identified standard 16550A-compatible 16-byte FIFO is batched. Other
/// identifications or enabled UART interrupts retain checked per-byte output.
/// </remarks>
internal static class OwnedCom1TextWriter
{
    private const ushort InterruptIdentificationRegisterPort = Com1OwnedPortIoBackend.BasePort + 2;
    private const byte DivisorLatchAccessMask = 0x80;
    private const byte FifoIdentificationMask = 0xe0;
    private const byte Fifo16Enabled = 0xc0;
    private const int Fifo16Bytes = 16;
    private const int PollsPerByte = PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit;

    // Fold into the existing non-inlined adapter boundary, avoiding an additional
    // retained frame on constrained diagnostic and interrupt stacks.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Write(in Com1OwnedPortIoBackend backend, string text)
    {
        if (text.Length == 0)
            return true;
        if (!backend.IsReady)
            return false;

        var lineControl = backend.ReadByte(Com1OwnedPortIoBackend.LineControlRegisterPort);
        if (!lineControl.Succeeded || (lineControl.Value & DivisorLatchAccessMask) != 0)
            return false;
        var interrupts = backend.ReadByte(Com1OwnedPortIoBackend.InterruptEnableRegisterPort);
        if (!interrupts.Succeeded)
            return false;

        int batchBytes = 1;
        // IIR can acknowledge THRE. Never read it with UART interrupts enabled,
        // and never reset or reconfigure the live transmitter.
        if (interrupts.Value == Com1OwnedPortIoBackend.DisableInterruptsValue)
        {
            var identification = backend.ReadByte(InterruptIdentificationRegisterPort);
            if (!identification.Succeeded)
                return false;
            if ((identification.Value & FifoIdentificationMask) == Fifo16Enabled)
                batchBytes = Fifo16Bytes;
        }

        ulong remainingPolls = (ulong)text.Length * PollsPerByte;
        int waitPolls = PollsPerByte;
        int position = 0;
        while (true)
        {
            // Keep readiness in this frame to bound the constrained stack prefix.
            bool ready = false;
            for (int poll = 0; poll < waitPolls && remainingPolls != 0; poll++)
            {
                remainingPolls--;
                var status = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                if (!status.Succeeded)
                    return false;
                if ((status.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0)
                {
                    ready = true;
                    break;
                }
            }

            if (!ready)
                return false;
            if (position == text.Length)
                return true;
            int count = text.Length - position < batchBytes ? text.Length - position : batchBytes;
            for (int i = 0; i < count; i++, position++)
                if (!backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, unchecked((byte)text[position])).Succeeded)
                    return false;

            waitPolls = count * PollsPerByte;
            // Return an empty FIFO to the following ordinary decimal/byte writer.
            if (position == text.Length && batchBytes == 1)
                return true;
        }
    }
}
