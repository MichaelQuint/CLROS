namespace OS.Kernel.Diagnostics;

/// <summary>Writes complete lowercase hex through the checked, exclusively owned COM1 backend.</summary>
/// <remarks>
/// The caller owns the readable source extent and the configured UART throughout
/// this synchronous call. Use only on the owning CPU with interrupts disabled
/// and no returning reentrant serial writer. A terminal fault/NMI may take over
/// COM1, abandon the incomplete record and halt; normal output never resumes
/// after that takeover. No complete normal-record claim survives it.
/// No pointers escape and no storage is
/// allocated. A false result may follow a written prefix; callers must invalidate
/// the enclosing evidence record and stop. This writer drops or retries no bytes.
/// The FIFO path requires a standard 16550A-compatible 16-byte transmitter with
/// working FIFO identification. Other identifications use the byte path.
/// </remarks>
internal static unsafe class OwnedCom1HexWriter
{
    private const ushort InterruptIdentificationRegisterPort = Com1OwnedPortIoBackend.BasePort + 2;
    private const byte DivisorLatchAccessMask = 0x80;
    private const byte FifoIdentificationMask = 0xe0;
    private const byte Fifo16Enabled = 0xc0;
    private const int Fifo16Bytes = 16;
    private const int PollsPerByte = PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit;

    internal static bool Write(in Com1OwnedPortIoBackend backend, byte* source, uint bytes)
    {
        if (bytes == 0)
            return true;
        if (source == null || !backend.IsReady)
            return false;

        var lineControl = backend.ReadByte(Com1OwnedPortIoBackend.LineControlRegisterPort);
        if (!lineControl.Succeeded || (lineControl.Value & DivisorLatchAccessMask) != 0)
            return false;
        var interrupts = backend.ReadByte(Com1OwnedPortIoBackend.InterruptEnableRegisterPort);
        if (!interrupts.Succeeded)
            return false;

        int batchBytes = 1;
        // Reading IIR can acknowledge a THRE interrupt. Do not touch it while
        // interrupts are enabled. Never reset or reconfigure a live FIFO.
        if (interrupts.Value == Com1OwnedPortIoBackend.DisableInterruptsValue)
        {
            var identification = backend.ReadByte(InterruptIdentificationRegisterPort);
            if (!identification.Succeeded)
                return false;
            if ((identification.Value & FifoIdentificationMask) == Fifo16Enabled)
                batchBytes = Fifo16Bytes;
        }

        ulong length = (ulong)bytes * 2;
        ulong remainingPolls = length * PollsPerByte;
        int waitPolls = PollsPerByte;
        ulong position = 0;
        while (true)
        {
            // Keep readiness in this frame: an extra call below every burst
            // exceeds the constrained diagnostic stack's conservative prefix.
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
            if (position == length)
                return true;
            int count = length - position < (ulong)batchBytes ? (int)(length - position) : batchBytes;
            for (int i = 0; i < count; i++, position++)
            {
                byte value = source[(uint)(position >> 1)];
                uint digit = (position & 1) == 0 ? (uint)value >> 4 : (uint)value & 15;
                byte character = (byte)(digit < 10 ? '0' + digit : 'a' + digit - 10);
                if (!backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, character).Succeeded)
                    return false;
            }

            // A full FIFO needs up to one original byte allowance per byte.
            // The entire call still has at most length*PollsPerByte LSR reads.
            waitPolls = count * PollsPerByte;
            // FIFO output takes one final readiness turn before returning the
            // exclusive UART to an ordinary single-byte writer.
            if (position == length && batchBytes == 1)
                return true;
        }
    }
}
