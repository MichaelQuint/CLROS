using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Net;
using Isn = OS.Net.TcpInitialSequence;

internal static unsafe class TcpInitialSequenceTests
{
    [HostTest("net.tcp.isn independent HMAC tuple domain and modular clock oracle", HostLane.Fast, 0)]
    private static void Oracle()
    {
        using var f = new Fixture();
        var lease = Isn.Acquire(f.Owner, f.Borrower, 0);
        HostCheck.True(lease.IsValid);
        ulong wrap = (1UL << 32) * 4000;
        ulong[] times = [Isn.QuietNanoseconds, Isn.QuietNanoseconds + 3999, Isn.QuietNanoseconds + 4000,
            wrap - 4000, wrap - 1, wrap, wrap + 3999, wrap + 4000];
        TcpConnectionKey[] tuples = [Key(), Key(localPort: 8081), Key(remotePort: 40001),
            new(new(10, 0, 3, 15), 8080, new(192, 0, 2, 9), 40000)];
        ulong issued = 0;
        foreach (ulong now in times)
            foreach (var tuple in tuples)
            {
                var result = Isn.Next(f.Owner, lease, tuple, now);
                HostCheck.Equal(Isn.Status.Success, result.Status);
                HostCheck.Equal(Expected(f.SecretBytes, tuple, now), result.Sequence);
                HostCheck.Equal(++issued, f.Owner->SequencesIssued);
            }
        using var zero = new Fixture();
        var zeroLease = Isn.Acquire(zero.Owner, zero.Borrower, 0);
        uint inverse = unchecked(0U - Prf(zero.SecretBytes, Key()));
        ulong zeroTime = ((1UL << 32) + inverse) * 4000;
        var zeroResult = Isn.Next(zero.Owner, zeroLease, Key(), zeroTime);
        HostCheck.Equal(Isn.Status.Success, zeroResult.Status);
        HostCheck.Equal(0U, zeroResult.Sequence);
        HostCheck.Equal(0U, Expected(zero.SecretBytes, Key(), zeroTime));
    }

    [HostTest("net.tcp.isn full quiet boundary backward clock and deadline overflow", HostLane.Fast, 1)]
    private static void Clocks()
    {
        using var f = new Fixture(); const ulong start = 12345;
        var lease = Isn.Acquire(f.Owner, f.Borrower, start);
        ulong deadline = start + Isn.QuietNanoseconds;
        HostCheck.Equal(deadline, f.Owner->QuietDeadline);
        HostCheck.Equal(Isn.Status.Quiet, Isn.Next(f.Owner, lease, Key(), deadline - 1).Status);
        HostCheck.Equal(0UL, f.Owner->SequencesIssued);
        byte[] before = f.Snapshot();
        HostCheck.Equal(Isn.Status.ClockFailure, Isn.Next(f.Owner, lease, Key(), deadline - 2).Status);
        f.Unchanged(before);
        var exact = Isn.Next(f.Owner, lease, Key(), deadline);
        HostCheck.Equal(Isn.Status.Success, exact.Status);
        HostCheck.Equal(Expected(f.SecretBytes, Key(), deadline), exact.Sequence);
        before = f.Snapshot();
        HostCheck.Equal(Isn.Status.ClockFailure, Isn.Next(f.Owner, lease, Key(), deadline - 1).Status);
        f.Unchanged(before);
        HostCheck.True(Isn.Release(f.Owner, lease)); before = f.Snapshot();
        HostCheck.True(!Isn.Acquire(f.Owner, f.Borrower, deadline - 1).IsValid); f.Unchanged(before);
        var again = Isn.Acquire(f.Owner, f.Borrower, deadline);
        HostCheck.Equal(deadline, f.Owner->QuietDeadline);
        HostCheck.Equal(Isn.Status.Success, Isn.Next(f.Owner, again, Key(), deadline).Status);

        using var high = new Fixture(); before = high.Snapshot();
        HostCheck.True(!Isn.Acquire(high.Owner, high.Borrower, ulong.MaxValue - Isn.QuietNanoseconds + 1).IsValid);
        high.Unchanged(before);
        var highLease = Isn.Acquire(high.Owner, high.Borrower, ulong.MaxValue - Isn.QuietNanoseconds);
        HostCheck.True(highLease.IsValid); HostCheck.Equal(ulong.MaxValue, high.Owner->QuietDeadline);
        HostCheck.Equal(Isn.Status.Quiet, Isn.Next(high.Owner, highLease, Key(), ulong.MaxValue - 1).Status);
        var last = Isn.Next(high.Owner, highLease, Key(), ulong.MaxValue);
        HostCheck.Equal(Isn.Status.Success, last.Status);
        HostCheck.Equal(Expected(high.SecretBytes, Key(), ulong.MaxValue), last.Sequence);
    }

    [HostTest("net.tcp.isn rejected admission aliases copied owners and invalid endpoints preserve bytes", HostLane.Fast, 2)]
    private static void Admission()
    {
        using var f = new Fixture(initialize: false); byte[] before = f.Snapshot();
        HostCheck.True(!Isn.Initialize(null, f.Secret, 7));
        HostCheck.True(!Isn.Initialize((Isn.Owner*)((byte*)f.Owner + 1), f.Secret, 7));
        HostCheck.True(!Isn.Initialize((Isn.Owner*)(ulong.MaxValue - 7), f.Secret, 7));
        HostCheck.True(!Isn.Initialize(f.Owner, null, 7));
        HostCheck.True(!Isn.Initialize(f.Owner, (byte*)(ulong.MaxValue - 15), 7));
        HostCheck.True(!Isn.Initialize(f.Owner, (byte*)f.Owner, 7));
        HostCheck.True(!Isn.Initialize(f.Owner, (byte*)f.Owner + sizeof(Isn.Owner) - 1, 7));
        HostCheck.True(!Isn.Initialize(f.Owner, f.Secret, 0)); f.Unchanged(before);
        ((byte*)f.Owner)[sizeof(Isn.Owner) - 1] = 1; before = f.Snapshot();
        HostCheck.True(!Isn.Initialize(f.Owner, f.Secret, 7)); f.Unchanged(before);
        ((byte*)f.Owner)[sizeof(Isn.Owner) - 1] = 0;
        HostCheck.True(Isn.Initialize(f.Owner, f.Secret, 7)); before = f.Snapshot();
        HostCheck.True(!Isn.Initialize(f.Owner, f.Secret, 8)); f.Unchanged(before);
        var lease = Isn.Acquire(f.Owner, f.Borrower, 0);
        Isn.Owner copy = *f.Owner; before = f.Snapshot();
        HostCheck.True(!Isn.IsInitialized(&copy)); HostCheck.True(!Isn.Current(&copy, lease));
        HostCheck.True(!Isn.Acquire(&copy, f.Borrower, 0).IsValid);
        HostCheck.Equal(Isn.Status.InvalidState, Isn.Next(&copy, lease, Key(), Isn.QuietNanoseconds).Status);
        HostCheck.True(!Isn.Release(&copy, lease)); HostCheck.True(!Isn.Erase(&copy)); f.Unchanged(before);
        TcpConnectionKey[] invalid = [default, Key(localPort: 0), Key(remotePort: 0),
            new(new(0, 0, 0, 1), 8080, new(10, 0, 2, 2), 40000),
            new(new(10, 0, 2, 15), 8080, new(224, 0, 0, 1), 40000)];
        foreach (var tuple in invalid)
        {
            HostCheck.Equal(Isn.Status.InvalidEndpoint, Isn.Next(f.Owner, lease, tuple, Isn.QuietNanoseconds).Status);
            f.Unchanged(before);
        }
        new Span<byte>(f.Secret, 32).Clear(); // Initialize copied the test seed; the caller may erase its original.
        var result = Isn.Next(f.Owner, lease, Key(), Isn.QuietNanoseconds);
        HostCheck.Equal(Isn.Status.Success, result.Status);
        HostCheck.Equal(Expected(f.SecretBytes, Key(), Isn.QuietNanoseconds), result.Sequence);
    }

    [HostTest("net.tcp.isn exact leases reject stale release and never wrap counters", HostLane.Fast, 3)]
    private static void Leases()
    {
        using var f = new Fixture(); byte[] before = f.Snapshot();
        HostCheck.True(!Isn.Acquire(f.Owner, null, 0).IsValid); f.Unchanged(before);
        var first = Isn.Acquire(f.Owner, f.Borrower, 100); var copied = first;
        before = f.Snapshot();
        HostCheck.True(!Isn.Acquire(f.Owner, (byte*)f.Borrower + 8, 100).IsValid); f.Unchanged(before);
        HostCheck.True(Isn.Current(f.Owner, copied)); HostCheck.True(Isn.Release(f.Owner, copied));
        HostCheck.True(!Isn.Current(f.Owner, first)); before = f.Snapshot();
        HostCheck.True(!Isn.Release(f.Owner, first));
        HostCheck.Equal(Isn.Status.InvalidState, Isn.Next(f.Owner, first, Key(), f.Owner->QuietDeadline).Status);
        f.Unchanged(before);
        var second = Isn.Acquire(f.Owner, (byte*)f.Borrower + 8, 101); before = f.Snapshot();
        HostCheck.True(second.IsValid && Isn.Current(f.Owner, second));
        HostCheck.True(!Isn.Release(f.Owner, first)); f.Unchanged(before);
        HostCheck.True(Isn.Release(f.Owner, second));
        Generation(ref *f.Owner) = ulong.MaxValue - 1;
        var last = Isn.Acquire(f.Owner, f.Borrower, 102);
        HostCheck.True(last.IsValid && Isn.Release(f.Owner, last)); before = f.Snapshot();
        HostCheck.True(!Isn.Acquire(f.Owner, f.Borrower, 102).IsValid); f.Unchanged(before);

        using var counter = new Fixture(); var lease = Isn.Acquire(counter.Owner, counter.Borrower, 0);
        Issued(ref *counter.Owner) = ulong.MaxValue - 1;
        HostCheck.Equal(Isn.Status.Success, Isn.Next(counter.Owner, lease, Key(), Isn.QuietNanoseconds).Status);
        HostCheck.Equal(ulong.MaxValue, counter.Owner->SequencesIssued); before = counter.Snapshot();
        var exhausted = Isn.Next(counter.Owner, lease, Key(), Isn.QuietNanoseconds + 4000);
        HostCheck.Equal(Isn.Status.Exhausted, exhausted.Status); HostCheck.Equal(0U, exhausted.Sequence);
        counter.Unchanged(before);
    }

    [HostTest("net.tcp.isn reseed needs drained owner fresh epoch changed key full quiet and erased owner", HostLane.Fast, 4)]
    private static void Reseed()
    {
        using var f = new Fixture(); var lease = Isn.Acquire(f.Owner, f.Borrower, 0);
        HostCheck.Equal(Isn.Status.Success, Isn.Next(f.Owner, lease, Key(), Isn.QuietNanoseconds).Status);
        f.Secret[31] ^= 0x5a; byte[] replacement = new ReadOnlySpan<byte>(f.Secret, 32).ToArray();
        byte[] before = f.Snapshot();
        HostCheck.True(!Isn.Reseed(f.Owner, f.Secret, 8)); HostCheck.True(!Isn.Erase(f.Owner)); f.Unchanged(before);
        HostCheck.True(Isn.Release(f.Owner, lease)); before = f.Snapshot();
        HostCheck.True(!Isn.Reseed(f.Owner, f.Secret, 7)); HostCheck.True(!Isn.Reseed(f.Owner, f.Secret, 0));
        HostCheck.True(!Isn.Reseed(f.Owner, null, 8));
        HostCheck.True(!Isn.Reseed(f.Owner, (byte*)f.Owner + sizeof(Isn.Owner) - 1, 8));
        f.Unchanged(before);
        f.Secret[31] ^= 0x5a; before = f.Snapshot();
        HostCheck.True(!Isn.Reseed(f.Owner, f.Secret, 8)); f.Unchanged(before);
        f.Secret[31] ^= 0x5a;
        HostCheck.True(Isn.Reseed(f.Owner, f.Secret, 8));
        HostCheck.Equal(1UL, f.Owner->SequencesIssued); HostCheck.Equal(0UL, f.Owner->QuietDeadline);
        HostCheck.True(!Isn.Current(f.Owner, lease)); before = f.Snapshot();
        HostCheck.True(!Isn.Acquire(f.Owner, f.Borrower, Isn.QuietNanoseconds - 1).IsValid); f.Unchanged(before);
        ulong restart = Isn.QuietNanoseconds + 4000;
        var renewed = Isn.Acquire(f.Owner, f.Borrower, restart);
        ulong deadline = restart + Isn.QuietNanoseconds;
        HostCheck.Equal(deadline, f.Owner->QuietDeadline);
        HostCheck.Equal(Isn.Status.Quiet, Isn.Next(f.Owner, renewed, Key(), deadline - 1).Status);
        var result = Isn.Next(f.Owner, renewed, Key(), deadline);
        HostCheck.Equal(Isn.Status.Success, result.Status);
        HostCheck.Equal(Expected(replacement, Key(), deadline), result.Sequence);
        before = f.Snapshot(); HostCheck.True(!Isn.Release(f.Owner, lease));
        HostCheck.True(!Isn.Erase(f.Owner)); f.Unchanged(before);
        HostCheck.True(Isn.Release(f.Owner, renewed)); HostCheck.True(Isn.Erase(f.Owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.Owner, sizeof(Isn.Owner)).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(!Isn.IsInitialized(f.Owner) && !Isn.Erase(f.Owner));
    }

    [HostTest("net.tcp.isn warmed repeated Next allocates no managed memory", HostLane.Fast, 5)]
    private static void Allocation()
    {
        using var f = new Fixture(); var lease = Isn.Acquire(f.Owner, f.Borrower, 0); var tuple = Key();
        HostCheck.Equal(Isn.Status.Success, Isn.Next(f.Owner, lease, tuple, Isn.QuietNanoseconds).Status);
        ulong now = Isn.QuietNanoseconds; bool success = true; uint actual = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint i = 0; i < 1000; i++)
        {
            now += 4000; var result = Isn.Next(f.Owner, lease, tuple, now);
            success &= result.Status == Isn.Status.Success; actual = result.Sequence;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal(0L, allocated); HostCheck.True(success);
        HostCheck.Equal(1001UL, f.Owner->SequencesIssued);
        HostCheck.Equal(Expected(f.SecretBytes, tuple, now), actual);
    }

    private static TcpConnectionKey Key(ushort localPort = 8080, ushort remotePort = 40000) =>
        new(new(10, 0, 2, 15), localPort, new(10, 0, 2, 2), remotePort);
    private static uint Expected(byte[] key, TcpConnectionKey tuple, ulong now) => unchecked((uint)(now / 4000) + Prf(key, tuple));
    private static uint Prf(byte[] key, TcpConnectionKey tuple)
    {
        byte[] domain = Encoding.ASCII.GetBytes("CLROS.TCP.ISN.v1"), message = new byte[domain.Length + 12];
        domain.CopyTo(message, 0); int offset = domain.Length;
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(offset), Pack(tuple.LocalAddress));
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(offset + 4), tuple.LocalPort);
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(offset + 6), Pack(tuple.RemoteAddress));
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(offset + 10), tuple.RemotePort);
        return BinaryPrimitives.ReadUInt32BigEndian(HMACSHA256.HashData(key, message));
    }
    private static uint Pack(Ipv4Address a) => (uint)a.Octet0 << 24 | (uint)a.Octet1 << 16 | (uint)a.Octet2 << 8 | a.Octet3;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Generation")]
    private static extern ref ulong Generation(ref Isn.Owner owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Issued")]
    private static extern ref ulong Issued(ref Isn.Owner owner);

    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        internal readonly Isn.Owner* Owner;
        internal readonly byte* Secret;
        internal readonly void* Borrower;
        // Deterministic entropy is exclusively a host-test input, never firmware or application admission evidence.
        internal readonly byte[] SecretBytes = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 3)).ToArray();
        internal Fixture(bool initialize = true)
        {
            HostCheck.True(sizeof(Isn.Owner) <= 128);
            memory = (byte*)NativeMemory.AlignedAlloc(256, 8); new Span<byte>(memory, 256).Clear();
            Owner = (Isn.Owner*)memory; Secret = memory + 128; Borrower = memory + 192;
            SecretBytes.CopyTo(new Span<byte>(Secret, 32));
            if (initialize) HostCheck.True(Isn.Initialize(Owner, Secret, 7));
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(memory, 256).ToArray();
        internal void Unchanged(byte[] before) => HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(memory, 256)));
        public void Dispose() { new Span<byte>(memory, 256).Clear(); SecretBytes.AsSpan().Clear(); NativeMemory.AlignedFree(memory); }
    }
}
