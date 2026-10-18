using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only finite vector call accounting. Parent syscall,
    /// scheduler, mapping, generation, native-source and packet joins remain
    /// mandatory; these numbers grant no execution or prepared-program authority.</summary>
    public readonly record struct SocketVectorsCallInspection(int Sends, int Receives, int ShortSends, int SendBlocked);

    /// <summary>Inspect supplied complete call/message rows for the four-peer
    /// vector fixture. Require exact remaining vector shapes, native short sends,
    /// send EAGAIN, reset, pending receive disposal and later health. Inputs are
    /// borrowed without mutation. Malformed or incomplete lifetimes throw.
    /// This synthetic seam does not qualify any guest or original program.</summary>
    public static SocketVectorsCallInspection InspectNativeAotSocketVectorsCallsForTests(string[] rows) => VectorSocketCalls(rows);

    /// <summary>Observed policy socket lifetimes before the first normal accept.
    /// The caller still needs original app/native, copied sockaddr and full
    /// runtime/network proof. These counts alone qualify no guest execution.</summary>
    public readonly record struct SocketPolicyCallInspection(int BindDenials,int DuplicateListeners,int CapacitySockets,int ClosedSockets);

    /// <summary>Check borrowed complete syscall returns for two denied binds,
    /// one competing listener and ten retained sockets exhausting the 16-fd budget
    /// after standard I/O, the runtime wake pipe and epoll port own fd0 through5.
    /// Require successful close/reuse before the normal listener/accept. Throws
    /// on malformed/missing/changed controls; no execution authority is produced.</summary>
    public static SocketPolicyCallInspection InspectNativeAotSocketPolicyCallsForTests(string[] rows)=>VectorSocketPolicyCalls(rows);

    /// <summary>Synthetic complete-leaf seam. Supplied ELF/predicates provide
    /// no sealed program, original package, packet or guest execution authority.
    /// Raw app framing, common runtime ownership and vector calls all must pass.</summary>
    public static ThreadingProtocolReport InspectNativeAotSocketVectorsProtocolForTests(byte[] elf, ReadOnlySpan<byte> rawTail,
        Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
    {
        var output = NativeAotThreadingOutput.Read(NativeAotSocketVectorsPublisher.Application, rawTail);
        var capture = NativeAotSocketGuest.ReadReadiness(output); string[] rows = Rows(output);
        var result = new ThreadingProtocol(NativeAotSocketVectorsPublisher.Application, rows, elf, instruction, syscallReturn,
            output.WriteLengths, userRoot, kernelRoot, socketCapture: capture).Validate();
        _ = VectorSocketCalls(rows); return result;
    }

    /// <summary>Validate the complete vector leaf using the canonical package,
    /// sealed original decoder, strict app output and common full runtime/socket
    /// replay. Parent firmware/NIC/packet/owned peer and original native joins
    /// remain mandatory. Caller retains all inputs; unsupported traces throw.
    /// This leaf alone grants no complete guest or backlog acceptance.</summary>
    public static void ValidateNativeAotSocketVectorsTail(string root, byte[] package, NativeAotSocketVectorsProgram program,
        ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root), program.SourceRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "socket_vectors_program_owner");
        NativeAotSocketVectorsPackage.RequireCurrentSources(root); NativeAotSocketVectorsPackage.RequirePackage(package);
        var output = NativeAotThreadingOutput.Read(NativeAotSocketVectorsPublisher.Application, rawTail);
        var capture = NativeAotSocketGuest.ReadReadiness(output); string[] rows = Rows(output);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotSocketVectorsPackage.Manifest);
        _ = NativeAotSocketVectorsOutput.Read(output.Stdout,requirePolicy:true);
        var owner = program!;
        _ = new ThreadingProtocol(NativeAotSocketVectorsPublisher.Application, rows, package[offset..],
            owner.IsInstruction, owner.IsSyscallReturn, output.WriteLengths, userRoot, kernelRoot, socketCapture: capture).Validate();
        _ = VectorSocketCalls(rows); NativeAotSocketVectorsPackage.RequireCurrentSources(root);
    }

    private sealed class VectorConnection(int index)
    {
        internal readonly int Index = index;
        internal uint Received, Sent;
        internal int Eof, Reset, Shutdown;
        internal bool Pending, Watched;
    }

    private static SocketPolicyCallInspection VectorSocketPolicyCalls(string[] rows)
    {
        Need(rows is not null && rows.Length is >0 and <=100000,"socket_policy_extent");
        var calls=ReadLinuxCallReturns(rows!,allowEpoll:true);HashSet<ulong> open=[];
        int binds=0,listens=0,created=0,closed=0,capacity=0;bool accepted=false;
        foreach(var call in calls.OrderBy(c=>c.RecordIndex))
        {
            var fields=Regex.Match(rows![call.RecordIndex],@"^M16_008_CALL identity=\d+ nr=(\d+) a0=(\d+) a1=(\d+) a2=(\d+) ",RegexOptions.CultureInvariant);
            Need(fields.Success,"socket_policy_fields");
            ulong number=ulong.Parse(fields.Groups[1].Value,CultureInfo.InvariantCulture),fd=ulong.Parse(fields.Groups[2].Value,CultureInfo.InvariantCulture);
            ulong a1=ulong.Parse(fields.Groups[3].Value,CultureInfo.InvariantCulture),a2=ulong.Parse(fields.Groups[4].Value,CultureInfo.InvariantCulture);
            long result=unchecked((long)call.ValueBits);
            if(number is 43 or 288)
            { Need(fd==6 && (result==-11 || result==7) && binds==5 && listens==2 && created==15 && closed==14 && capacity==10 && open.SetEquals(new ulong[]{6}),"socket_policy_followup");accepted=true;break; }
            if(number==41 && fd==2 && (a1&15)==1 && a2==6)
            {
                if(result==-24)
                { Need(capacity==0 && binds==4 && listens==1 && open.SetEquals(Enumerable.Range(6,10).Select(i=>(ulong)i)),"socket_policy_capacity");capacity=10;continue; }
                Need(result is >=6 and <16 && open.Add((ulong)result),"socket_policy_create");created++;
                if(capacity!=0)Need(created==15 && closed==14 && open.SetEquals(new ulong[]{6}),"socket_policy_reuse");
            }
            else if(number==49)
            {
                long[] expected=[-13,-13,0,-98,0];
                Need(binds<expected.Length && result==expected[binds] && a2==16 && open.Contains(fd) && fd==(binds==3?7UL:6UL),"socket_policy_bind");
                if(binds<2)Need(open.Count==1 && created==binds+1 && closed==binds,"socket_policy_denied_lifetime");
                if(binds==2)Need(open.Count==1 && created==3 && closed==2,"socket_policy_first_listener");
                if(binds==3)Need(listens==1 && open.Count==2 && created==4 && closed==2,"socket_policy_duplicate");
                if(binds==4)Need(capacity==10 && closed==14 && open.Count==1,"socket_policy_main_bind");
                binds++;
            }
            else if(number==50)
            { Need(result==0 && fd==6 && open.Contains(fd) && (listens==0?binds==3:binds==5) && listens<2,"socket_policy_listen");listens++; }
            else if(number==3 && open.Contains(fd))
            { Need(result==0,"socket_policy_close");open.Remove(fd);closed++; }
        }
        Need(accepted,"socket_policy_missing_followup");return new(2,1,capacity,closed);
    }

    private static SocketVectorsCallInspection VectorSocketCalls(string[] rows)
    {
        Need(rows is not null && rows.Length is > 0 and <= 100000, "socket_vectors_call_extent");
        var returns = ReadLinuxCallReturns(rows!, allowEpoll: true);
        var messages = NativeAotSocketMessages.Read(rows!, returns.ToDictionary(call => call.RecordIndex)).ToDictionary(message => message.CallRecordIndex);
        Dictionary<ulong, VectorConnection> active = []; int accepted = 0, closed = 0, sends = 0, receives = 0, shortSends = 0, blocked = 0;
        int twoSends = 0, twoReceives = 0, partialReceives = 0;
        foreach (var call in returns.OrderBy(call => call.RecordIndex))
        {
            var match = Regex.Match(rows![call.RecordIndex], @"^M16_008_CALL identity=\d+ nr=(\d+) a0=(\d+) a1=(\d+) a2=(\d+) ", RegexOptions.CultureInvariant);
            Need(match.Success, "socket_vectors_call_fields");
            ulong number = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), fd = ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            ulong arg1 = ulong.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture); long result = unchecked((long)call.ValueBits);
            ulong arg2 = ulong.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            if (number is 43 or 288 && result != -11)
            {
                Need(result is >= 3 and < 16 && accepted < 4 && active.Count == 0 &&
                    active.TryAdd((ulong)result, new(accepted++)), "socket_vectors_accept"); continue;
            }
            if (number == 233 && arg1 is 1 or 3 && result == 0 && active.TryGetValue(arg2, out var watched) && watched.Index == 2)
                watched.Watched = true; // The common replay authenticates the actual port/target lifetime.
            if (!active.TryGetValue(fd, out var connection))
            { Need(number is not (46 or 47), "socket_vectors_message_owner"); continue; }
            int index = connection.Index;
            if (number == 3)
            {
                Need(result == 0 && connection.Received == 64 && connection.Sent == (index == 0 ? 524288 : index == 3 ? 64 : 0) &&
                    connection.Eof == (index is 0 or 3 ? 1 : 0) && connection.Reset == (index == 1 ? 1 : 0) &&
                    connection.Shutdown == (index is 0 or 3 ? 1 : 0) && connection.Pending == (index == 2) &&
                    (index != 2 || connection.Watched), "socket_vectors_close");
                active.Remove(fd); closed++; continue;
            }
            if (number == 48)
            {
                Need(index is 0 or 3 && arg1 == 1 && result == 0 && connection.Shutdown++ == 0 && connection.Eof == 1 &&
                    connection.Sent == (index == 0 ? 524288 : 64), "socket_vectors_shutdown"); continue;
            }
            if (number is not (44 or 45 or 46 or 47)) continue; // Common replay validates options/control calls.
            Need(number is 46 or 47 && messages.TryGetValue(call.RecordIndex, out _), "socket_vectors_message_required");
            var message = messages[call.RecordIndex]; Need(message.Flags == 0 && connection.Reset == 0 && connection.Shutdown == 0, "socket_vectors_io_state");
            bool send = number == 46;
            if (send)
            {
                Need(index is 0 or 3 && connection.Received == 64 && connection.Eof == 1, "socket_vectors_send_phase"); sends++;
                uint expected = index == 0 ? 524288u : 64u, batch = Math.Min(65536u, expected), split = batch / 3;
                uint sent = connection.Sent % batch, remaining = batch - sent;
                Need(connection.Sent < expected,"socket_vectors_send_extent");
                VectorShape(message, sent < split ? split - sent : remaining,
                    sent < split ? batch - split : 0);
                Need(result == -11 || result is > 0 and <= 8192 && (ulong)result <= remaining, "socket_vectors_send_result");
                if (result == -11) { Need(index == 0, "socket_vectors_send_block_owner"); blocked++; }
                else
                {
                    if (result < remaining) shortSends++;
                    if (message.Vectors.Count == 2) twoSends++;
                    connection.Sent += (uint)result;
                }
            }
            else
            {
                receives++; Need(result == -11 || result == -104 && index == 1 || result is >= 0 and <= 64, "socket_vectors_receive_result");
                if (connection.Received < 64)
                {
                    uint remaining = 64 - connection.Received;
                    VectorShape(message, connection.Received < 17 ? 17 - connection.Received : remaining,
                        connection.Received < 17 ? 47u : 0);
                    Need(result == -11 || result > 0 && result <= remaining, "socket_vectors_request_result");
                    if (result > 0)
                    {
                        Need(index != 0 || connection.Received >= 3 || (ulong)result <= 3 - connection.Received, "socket_vectors_first_partial");
                        if (result < remaining) partialReceives++;
                        if (message.Vectors.Count == 2) twoReceives++;
                        connection.Received += (uint)result;
                    }
                }
                else if (index == 2)
                {
                    VectorShape(message, 3, 3, gap: 2);
                    Need(result == -11, "socket_vectors_pending_receive"); connection.Pending = true;
                }
                else
                {
                    VectorShape(message, 1, 1, gap: 1);
                    Need(connection.Eof == 0 && (index == 1 ? result is -11 or -104 : result is -11 or 0), "socket_vectors_terminal_receive");
                    if (result == 0) connection.Eof++;
                    if (result == -104) connection.Reset++;
                }
            }
        }
        Need(accepted == 4 && closed == 4 && active.Count == 0 && twoSends > 0 && twoReceives > 0 && partialReceives > 0 &&
            shortSends > 0 && blocked > 0, "socket_vectors_complete_calls");
        return new(sends, receives, shortSends, blocked);
    }

    private static void VectorShape(NativeAotSocketMessages.Message message, uint first, uint second, uint gap = 0)
    {
        Need(first > 0 && message.Total == first + second && message.Vectors.Count == (second == 0 ? 1 : 2) &&
            message.Vectors[0].Bytes == first && (second == 0 || message.Vectors[1].Bytes == second &&
                message.Vectors[0].Address <= ulong.MaxValue - first - gap &&
                message.Vectors[1].Address == message.Vectors[0].Address + first + gap), "socket_vectors_remaining_shape");
    }
}
