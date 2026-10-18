using OS.Tooling;

internal static class NativeAotSocketVectorsCallTests
{
    // Independent policy-only prefix; it provides no original guest authority.
    private static string[] PolicyCalls(string change = "")
    {
        List<string> rows = []; uint ordinal = 0;
        void Call(uint number, uint fd, long result, ulong arg1 = 0, ulong arg2 = 0)
        {
            ordinal++; ulong value = unchecked((ulong)result);
            rows.Add($"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1={arg1} a2={arg2} a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={value} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}");
        }
        void Create(long result) => Call(41, 2, result, 524289, 6);
        void Bind(uint fd, long result) => Call(49, fd, result, 8192, change == "short_sockaddr" ? 8u : 16u);
        void Close(uint fd) => Call(3, fd, change == "failed_close" ? -9 : 0);
        Call(3, 3, 0); // Startup package view closed before runtime descriptors.
        Call(41, 10, -97, 524290); // Unsupported IPv6 capability query.
        Create(6); Bind(change == "unknown_bind" ? 7u : 6u, change == "wrong_denial" ? -98 : -13);
        if (change != "missing_denied_close") Close(6);
        Create(6); Bind(6, -13); Close(6);
        Create(6); Bind(6, 0); Call(50, 6, 0, 1);
        Create(change == "alias" ? 6 : 7); Bind(7, change == "duplicate_admitted" ? 0 : -98);
        Close(7); Close(6);
        for (uint fd = 6; fd < (change == "short_capacity" ? 15 : 16); fd++) Create(fd);
        if (change != "missing_exhaustion") Create(change == "wrong_exhaustion" ? -105 : -24);
        if (change == "repeated_exhaustion") Create(-24);
        if (change == "early_accept") Call(43, 6, -11);
        for (uint fd = 15; fd >= 6; fd--) if (change != "retained_capacity" || fd != 15) Close(fd);
        Create(6); Bind(6, 0);
        if (change != "missing_main_listen") Call(50, 6, 0, 4);
        if (change != "missing_accept") Call(43, 6, -11);
        return rows.ToArray();
    }

    [HostTest("m10-008 socket policy calls require real errno cleanup capacity and listener recovery")]
    private static void PolicyValid()
    {
        string[] rows = PolicyCalls(), before = (string[])rows.Clone();
        var result = QemuTranscripts.InspectNativeAotSocketPolicyCallsForTests(rows);
        HostCheck.Equal(2, result.BindDenials); HostCheck.Equal(1, result.DuplicateListeners);
        HostCheck.Equal(10, result.CapacitySockets); HostCheck.Equal(14, result.ClosedSockets);
        HostCheck.True(rows.SequenceEqual(before));
    }

    [HostTest("m10-008 socket policy calls reject missing controls altered errors and leaked owners")]
    private static void PolicyInvalid()
    {
        foreach (string change in new[] { "short_sockaddr", "unknown_bind", "wrong_denial", "missing_denied_close",
            "alias", "duplicate_admitted", "short_capacity", "missing_exhaustion", "wrong_exhaustion",
            "repeated_exhaustion", "early_accept", "retained_capacity", "failed_close", "missing_main_listen", "missing_accept" })
        {
            bool rejected = false;
            try { _ = QemuTranscripts.InspectNativeAotSocketPolicyCallsForTests(PolicyCalls(change)); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, change);
        }
    }

    // Independent finite call observations, without a guest, mappings or source authority.
    private static string[] Calls(string change = "")
    {
        List<string> rows = []; uint ordinal = 0, generation = 0;
        void Call(uint number, uint fd, long result, ulong arg1 = 0, ulong arg2 = 0)
        {
            ordinal++; ulong value = unchecked((ulong)result);
            rows.Add($"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1={arg1} a2={arg2} a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={value} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}");
        }
        void Message(bool send, uint first, uint second, long result)
        {
            uint number = send ? 46u : 47u;
            if (change != "missing_snapshot" || !send)
            {
                uint firstBytes = first, secondBytes = second;
                if (change == "wrong_split" && send && first == 21845) { firstBytes--; secondBytes++; }
                rows.Add($"M16_013_MESSAGE identity=4294967297 ordinal={ordinal + 1} nr={number} fd=6 generation={generation} header=8192 flags=0 table=12288 vectors={(second == 0 ? 1 : 2)} total={first + second}");
                ulong address = change == "wrapped_gap" && !send && first == 3 && second == 3 ? ulong.MaxValue - 3 : 16384;
                rows.Add($"M16_013_VECTOR identity=4294967297 ordinal={ordinal + 1} index=0 address={address} bytes={firstBytes}");
                uint gap = !send && first == second ? first == 3 ? 2u : 1u : 0;
                ulong secondAddress = unchecked(address + firstBytes + gap + (change == "noncontiguous" && send ? 1u : 0));
                if (second != 0) rows.Add($"M16_013_VECTOR identity=4294967297 ordinal={ordinal + 1} index=1 address={secondAddress} bytes={secondBytes}");
            }
            Call(number, 6, result, 8192);
        }
        for (int index = 0; index < 4; index++)
        {
            generation++; Call(43, 5, 6);
            if (index == 0 && change == "split_gate") { Message(false, 17, 47, 1); Message(false, 16, 47, 2); }
            else Message(false, 17, 47, index == 0 ? change == "wrong_first_partial" ? 4 : 3 : 64);
            if (index == 0) Message(false, 14, 47, 61);
            if (index == 2)
            {
                if (change != "missing_pending") Message(false, 3, 3, change == "pending_eof" ? 0 : -11);
                if (change == "repeated_pending") Message(false, 3, 3, -11);
                if (change != "missing_watch") Call(233, 5, 0, 1, 6);
            }
            else
            {
                Message(false, 1, 1, index == 1 ? change == "reset_eof" ? 0 : -104 : 0);
                if (index == 1 && change == "after_reset") Message(false, 1, 1, -11);
                if (index != 1)
                {
                    uint expected = index == 0 ? 524288u : 64u, batch = Math.Min(65536u, expected), split = batch / 3;
                    for (uint sent = 0; sent < (change == "truncated_response" && index == 0 ? batch : expected);)
                    {
                        uint position = sent % batch;
                        uint first = position < split ? split - position : batch - position, second = position < split ? batch - split : 0;
                        if (sent == 8192 && change != "missing_send_block") Message(true, first, second, -11);
                        uint count = Math.Min(8192u, batch - position);
                        Message(true, first, second, change == "oversized_return" ? 8193 : count); sent += count;
                    }
                    Call(48, 6, 0, change == "wrong_shutdown" ? 2u : 1u);
                }
            }
            Call(3, 6, 0);
        }
        if (change == "fifth_connection") Call(43, 5, 6);
        return rows.ToArray();
    }

    [HostTest("m16-013 native vector calls retain partial-send remainders blocking reset cancellation and health")]
    private static void Valid()
    {
        string[] rows = Calls(), before = (string[])rows.Clone();
        var result = QemuTranscripts.InspectNativeAotSocketVectorsCallsForTests(rows);
        HostCheck.Equal(66, result.Sends); HostCheck.Equal(9, result.Receives);
        HostCheck.Equal(56, result.ShortSends); HostCheck.Equal(1, result.SendBlocked);
        HostCheck.True(rows.SequenceEqual(before));
        HostCheck.Equal(10, QemuTranscripts.InspectNativeAotSocketVectorsCallsForTests(Calls("repeated_pending")).Receives);
        HostCheck.Equal(10, QemuTranscripts.InspectNativeAotSocketVectorsCallsForTests(Calls("split_gate")).Receives);
    }

    [HostTest("m16-013 native vector calls reject changed shapes missing native progress and incomplete lifetimes")]
    private static void Invalid()
    {
        foreach (string change in new[] { "missing_snapshot", "wrong_split", "noncontiguous", "wrong_first_partial", "missing_pending",
            "pending_eof", "wrapped_gap", "missing_watch", "reset_eof", "after_reset", "missing_send_block", "oversized_return", "wrong_shutdown", "fifth_connection", "truncated_response" })
        {
            bool rejected = false; try { _ = QemuTranscripts.InspectNativeAotSocketVectorsCallsForTests(Calls(change)); }
            catch (ToolFailure) { rejected = true; } HostCheck.True(rejected, change);
        }
        bool incomplete = false;
        try { _ = QemuTranscripts.InspectNativeAotSocketVectorsCallsForTests(Calls()[..^1]); } catch (ToolFailure) { incomplete = true; }
        HostCheck.True(incomplete);
    }
}
