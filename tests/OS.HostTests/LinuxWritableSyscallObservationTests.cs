using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using O = OS.Kernel.Compatibility.LinuxX64.LinuxWritableSyscallObservation;

// Copied-value/owned-buffer controls only. No actual syscall, user copy,
// scheduler, native publication, device or persistence authority is exercised.
internal static unsafe partial class LinuxWritableSyscallObservationTests
{
    private const ulong Epoch = 99;
    private static O.Admission Admission => new()
    {
        DescriptorOwner = 0x10000,
        AttachmentOwner = 0x20000,
        AttachmentEpoch = 77,
        ObservationEpoch = Epoch
    };

    private static O.CallIdentity Call(ulong nr = 2) => new()
    {
        DescriptorOwner = 0x10000,
        AttachmentOwner = 0x20000,
        AttachmentEpoch = 77,
        ThreadIdentity = 0x100000001,
        Ordinal = 1,
        Number = nr,
        Rip = 0x401002
    };
    private static O.RequestIdentity Request => new()
    {
        Owner = 0x20000,
        Epoch = 77,
        Generation = 31
    };

    private static byte[] Data(int count) => Enumerable.Range(0, count).Select(i => unchecked((byte)(i * 73 + 19))).ToArray();
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Sequence")]
    private static extern ref ulong Sequence(ref O.State s);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Self")]
    private static extern ref ulong Self(ref O.State s);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref O.Admission Input(ref O.State s);
    private static O.Header Peek(Fixture f, out byte[] bytes)
    {
        byte[] target = new byte[O.PayloadCapacity];
        O.Header header = default;
        HostCheck.Equal(O.Status.Success, O.CopyNext(f.State, Epoch, &header, (uint)sizeof(O.Header), target));
        bytes = target[..(int)header.PayloadBytes];
        return header;
    }

    private static O.Status Path(Fixture f, byte[]? bytes = null) => O.CapturePath(f.State, Epoch, Call(), default, 0x500000, bytes ?? "/data/LOG.TXT\0"u8.ToArray());
    private static void Drain(Fixture f)
    {
        while (f.State->PendingRecords != 0)
        {
            var h = Peek(f, out _);
            HostCheck.Equal(O.Status.Success, O.Acknowledge(f.State, Epoch, h.Sequence));
        }
    }

    [HostTest("writable syscall observation has one measured POD area and rejects dirty admission")]
    private static void Layout()
    {
        HostCheck.Equal(144, sizeof(O.Header));
        HostCheck.Equal(8544, sizeof(O.State));
        HostCheck.Equal(O.Status.InvalidStorage, O.Start((O.State*)1, Admission));
        HostCheck.Equal(O.Status.InvalidStorage, O.Start((O.State*)(ulong.MaxValue - 7), Admission));
        using var f = new Fixture(false);
        byte[] original = f.Snapshot();
        O.Admission bad = Admission;
        bad.AttachmentEpoch = 0;
        HostCheck.Equal(O.Status.InvalidRecord, O.Start(f.State, bad));
        f.Same(original);
        ((byte*)f.State)[sizeof(O.State) - 1] = 1;
        byte[] dirty = f.Snapshot();
        HostCheck.Equal(O.Status.InvalidStorage, O.Start(f.State, Admission));
        f.Same(dirty);
        ((byte*)f.State)[sizeof(O.State) - 1] = 0;
        HostCheck.Equal(O.Status.Success, O.Start(f.State, Admission));
        HostCheck.True(f.State->Active && !f.State->Retired && !f.State->EvidenceLost && f.State->PendingRecords == 0);
    }

    [HostTest("writable syscall observation structural ranges exclude exact partial covering and wrapping aliases")]
    private static void Ranges()
    {
        using var f = new Fixture();
        ulong p = (ulong)f.State, n = (ulong)sizeof(O.State);
        byte[] before = f.Snapshot();
        HostCheck.True(O.CanRetainStorage(f.State, Epoch, p - 1, 1) && O.CanRetainStorage(f.State, Epoch, p + n, 1));
        foreach (var range in new[]
        {
            (p, n),
            (p - 1, 2UL),
            (p + n - 1, 2UL),
            (p - 1, n + 2),
            (ulong.MaxValue - 3, 8UL),
            (p + n, 0UL)
        }

        )
            HostCheck.True(!O.CanRetainStorage(f.State, Epoch, range.Item1, range.Item2));
        HostCheck.True(!O.CanRetainStorage(f.State, Epoch + 1, p + n, 1));
        f.Same(before);
        HostCheck.Equal(O.Status.Success, O.Stop(f.State, Epoch));
        HostCheck.True(!O.CanRetainStorage(f.State, Epoch, p + n, 1));
    }

    [HostTest("writable syscall observation copies maximum raw nul terminated path without decoding or retaining source")]
    private static void PathBytes()
    {
        using var f = new Fixture();
        byte[] path = Enumerable.Repeat((byte)0xff, 256).ToArray();
        path[^1] = 0;
        byte[] expected = path.ToArray();
        HostCheck.Equal(O.Status.Success, Path(f, path));
        path.AsSpan().Fill(42);
        var h = Peek(f, out var bytes);
        HostCheck.True(bytes.SequenceEqual(expected) && h.Kind == O.Kind.Path && h.Boundary == O.Boundary.Admission && h.Request.Generation == 0 && h.Request.Owner == 0 && h.Request.Epoch == 0 && h.Sequence == 1 && h.Call.Ordinal == 1 && h.UserAddress == 0x500000 && h.RequestedBytes == 256);
        HostCheck.Equal(O.Status.Busy, O.Stop(f.State, Epoch));
        Drain(f);
    }

    [HostTest("writable syscall observation malformed path and source overlap fail before any write")]
    private static void InvalidPaths()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        foreach (byte[] bytes in new[]
        {
            Array.Empty<byte>(),
            new byte[]
            {
                0
            },
            new byte[]
            {
                1,
                2
            },
            new byte[]
            {
                1,
                0,
                0
            },
            new byte[257]
        }

        )
        {
            HostCheck.Equal(O.Status.InvalidRecord, Path(f, bytes));
            f.Same(before);
        }

        HostCheck.Equal(O.Status.InvalidRecord, O.CapturePath(f.State, Epoch, Call(), default, ulong.MaxValue, "a\0"u8));
        HostCheck.Equal(O.Status.InvalidStorage, O.CapturePath(f.State, Epoch, Call(), default, 0x500000, new ReadOnlySpan<byte>(f.State, 2)));
        HostCheck.Equal(O.Status.InvalidStorage, O.CapturePath(f.State, Epoch, Call(), default, 0x500000, new ReadOnlySpan<byte>((byte*)f.State - 1, 2)));
        f.Same(before);
    }

    [HostTest("writable syscall observation preserves full copied pwrite vector payload and zero input without user reread")]
    private static void PwriteBytes()
    {
        using var f = new Fixture();
        byte[] input = Data(8192), expected = input.ToArray();
        HostCheck.Equal(O.Status.Success, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 8192, long.MaxValue, input));
        input.AsSpan().Clear();
        var h = Peek(f, out var bytes);
        HostCheck.True(bytes.SequenceEqual(expected) && h.Kind == O.Kind.PwriteInput && h.VectorBase == 0x600000 && h.UserAddress == 0x500000 && h.RequestedBytes == 8192 && h.FileOffset == long.MaxValue && h.ValueBits == 0 && h.Request.Generation == 0);
        HostCheck.Equal(O.Status.Success, O.Acknowledge(f.State, Epoch, h.Sequence));
        HostCheck.Equal(O.Status.Success, O.CapturePwrite(f.State, Epoch, Call(328), Request, 0x500000, ulong.MaxValue, 0, 4094, []));
        h = Peek(f, out bytes);
        HostCheck.True(bytes.Length == 0 && h.VectorBase == ulong.MaxValue && h.Sequence == 2 && h.Request.Generation == 31);
        Drain(f);
    }

    [HostTest("writable syscall observation rejects invalid copied pwrite counts offsets ranges and scratch aliases")]
    private static void InvalidPwrite()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        byte[] bytes = Data(16);
        HostCheck.Equal(O.Status.InvalidRecord, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 17, 0, bytes));
        HostCheck.Equal(O.Status.InvalidRecord, O.CapturePwrite(f.State, Epoch, Call(328), default, ulong.MaxValue, 0x600000, 16, 0, bytes));
        HostCheck.Equal(O.Status.InvalidRecord, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, ulong.MaxValue, 16, 0, bytes));
        HostCheck.Equal(O.Status.InvalidRecord, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 16, (ulong)long.MaxValue + 1, bytes));
        HostCheck.Equal(O.Status.InvalidStorage, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 16, 0, new ReadOnlySpan<byte>(f.State, 16)));
        HostCheck.Equal(O.Status.InvalidStorage, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 16, 0, new ReadOnlySpan<byte>((void*)(ulong.MaxValue - 7), 16)));
        f.Same(before);
    }

    [HostTest("writable syscall observation positive read retains original destination request ceiling and exact bytes")]
    private static void ReadBytes()
    {
        using var f = new Fixture();
        byte[] source = Data(521), expected = source.ToArray();
        HostCheck.Equal(O.Status.Success, O.CaptureRead(f.State, Epoch, Call(17), Request, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x700000, 65536, 521, 521, source));
        source.AsSpan().Clear();
        var h = Peek(f, out var bytes);
        HostCheck.True(bytes.SequenceEqual(expected) && h.Request.Generation == 31 && h.ValueBits == 521 && h.OutputPrefix == 521 && h.RequestedBytes == 65536 && h.UserAddress == 0x700000 && h.Kind == O.Kind.ReadOutput && h.Boundary == O.Boundary.CompletionOutput);
        Drain(f);
    }

    [HostTest("writable syscall observation failed cancelled revoked repark and eof never publish copyout")]
    private static void NoFailedOutput()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        foreach (O.CopyDisposition d in new[]
        {
            O.CopyDisposition.Uncommitted,
            O.CopyDisposition.Failed,
            O.CopyDisposition.Cancelled,
            O.CopyDisposition.Revoked,
            O.CopyDisposition.Repark
        }

        )
        {
            // Poisoned source values must not be read on a noncommitted boundary.
            var poison = new ReadOnlySpan<byte>((void*)(ulong.MaxValue - 7), 16);
            HostCheck.Equal(O.Status.NotRecorded, O.CaptureRead(f.State, Epoch, Call(0), Request, O.Boundary.CompletionOutput, d, 0x700000, 16, 16, -4, poison));
            HostCheck.Equal(O.Status.NotRecorded, O.CaptureStat(f.State, Epoch, Call(5), Request, O.Boundary.CompletionOutput, d, 0x700000, 144, -14, poison));
            f.Same(before);
        }

        HostCheck.Equal(O.Status.NotRecorded, O.CaptureRead(f.State, Epoch, Call(0), default, O.Boundary.ImmediateOutput, O.CopyDisposition.Committed, 0x700000, 1, 1, 0, []));
        f.Same(before);
    }

    [HostTest("writable syscall observation read prefix and exact stat size cannot grow at completion")]
    private static void OutputBounds()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        foreach (var test in new[]
        {
            (16UL, 15U, 16L, 16),
            (15UL, 16U, 16L, 16),
            (16UL, 16U, 15L, 16),
            (65537UL, 16U, 16L, 16)
        }

        )
            HostCheck.Equal(O.Status.InvalidRecord, O.CaptureRead(f.State, Epoch, Call(0), Request, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x700000, test.Item1, test.Item2, test.Item3, Data(test.Item4)));
        HostCheck.Equal(O.Status.InvalidRecord, O.CaptureRead(f.State, Epoch, Call(0), default, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x700000, 16, 16, 16, Data(16)));
        foreach (int count in new[]
        {
            143,
            145
        }

        )
            HostCheck.Equal(O.Status.InvalidRecord, O.CaptureStat(f.State, Epoch, Call(5), Request, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x700000, 144, 0, Data(count)));
        HostCheck.Equal(O.Status.InvalidRecord, O.CaptureStat(f.State, Epoch, Call(5), Request, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x700000, 143, 0, Data(144)));
        f.Same(before);
    }

    [HostTest("writable syscall observation records both immediate and deferred exact stat wire")]
    private static void StatBytes()
    {
        using var f = new Fixture();
        byte[] wire = Data(144), expected = wire.ToArray();
        HostCheck.Equal(O.Status.Success, O.CaptureStat(f.State, Epoch, Call(4), default, O.Boundary.ImmediateOutput, O.CopyDisposition.Committed, 0x700000, 144, 0, wire));
        HostCheck.Equal(O.Status.Success, O.CaptureStat(f.State, Epoch, Call(5), Request, O.Boundary.CompletionOutput, O.CopyDisposition.Committed, 0x710000, 144, 0, wire));
        wire.AsSpan().Clear();
        var first = Peek(f, out var bytes);
        HostCheck.True(bytes.SequenceEqual(expected) && first.Boundary == O.Boundary.ImmediateOutput && first.Request.Generation == 0 && first.ValueBits == 0 && first.PayloadBytes == 144);
        HostCheck.Equal(O.Status.Success, O.Acknowledge(f.State, Epoch, first.Sequence));
        var second = Peek(f, out bytes);
        HostCheck.True(bytes.SequenceEqual(expected) && second.Sequence == 2 && second.Request.Generation == 31 && second.PayloadOffset == 144);
        Drain(f);
    }

    [HostTest("writable syscall observation rejects foreign partial request identity and stale copied call generations")]
    private static void Identities()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        for (int change = 0; change < 7; change++)
        {
            var c = Call();
            switch (change)
            {
                case 0:
                    c.DescriptorOwner++;
                    break;
                case 1:
                    c.AttachmentOwner++;
                    break;
                case 2:
                    c.AttachmentEpoch++;
                    break;
                case 3:
                    c.ThreadIdentity = 1;
                    break;
                case 4:
                    c.ThreadIdentity = 0x100000011;
                    break;
                case 5:
                    c.Ordinal = 4097;
                    break;
                case 6:
                    c.Rip = 0x1000000000;
                    break;
            }

            HostCheck.Equal(O.Status.InvalidRecord, O.CapturePath(f.State, Epoch, c, default, 0x500000, "x\0"u8));
            f.Same(before);
        }

        foreach (var r in new[]
        {
            new O.RequestIdentity
            {
                Generation = 1
            },
            new O.RequestIdentity
            {
                Owner = 0x20000,
                Epoch = 77
            },
            new O.RequestIdentity
            {
                Owner = 0x30000,
                Epoch = 77,
                Generation = 1
            },
            new O.RequestIdentity
            {
                Owner = 0x20000,
                Epoch = 78,
                Generation = 1
            }
        }

        )
        {
            HostCheck.Equal(O.Status.InvalidRecord, O.CapturePath(f.State, Epoch, Call(), r, 0x500000, "x\0"u8));
            f.Same(before);
        }

        HostCheck.Equal(O.Status.StaleEpoch, O.CapturePath(f.State, Epoch + 1, Call(), default, 0x500000, "x\0"u8));
        f.Same(before);
    }

    [HostTest("writable syscall observation overflow retains original two records and permanent evidence failure")]
    private static void RecordOverflow()
    {
        using var f = new Fixture();
        HostCheck.Equal(O.Status.Success, Path(f));
        HostCheck.Equal(O.Status.Success, O.CaptureStat(f.State, Epoch, Call(4), default, O.Boundary.ImmediateOutput, O.CopyDisposition.Committed, 0x700000, 144, 0, Data(144)));
        var original = Peek(f, out var payload);
        HostCheck.Equal(O.Status.CapacityExceeded, Path(f));
        HostCheck.True(f.State->EvidenceLost && f.State->FailureReason == O.EvidenceFailure.Capacity && f.State->PendingRecords == 2);
        var current = Peek(f, out var retained);
        HostCheck.True(current.Sequence == original.Sequence && retained.SequenceEqual(payload));
        Drain(f);
        HostCheck.Equal(O.Status.Incomplete, Path(f));
        HostCheck.Equal(O.Status.Success, O.Stop(f.State, Epoch));
        HostCheck.True(f.State->Retired && f.State->EvidenceLost);
    }

    [HostTest("writable syscall observation shared payload arena refuses partial truncation")]
    private static void PayloadOverflow()
    {
        using var f = new Fixture();
        byte[] data = Data(8192);
        HostCheck.Equal(O.Status.Success, O.CapturePwrite(f.State, Epoch, Call(328), default, 0x500000, 0x600000, 8192, 0, data));
        HostCheck.Equal(O.Status.CapacityExceeded, Path(f, "x\0"u8.ToArray()));
        var h = Peek(f, out var bytes);
        HostCheck.True(bytes.SequenceEqual(data) && h.PayloadBytes == 8192 && f.State->PendingRecords == 1);
        Drain(f);
    }

    [HostTest("writable syscall observation peek validates complete output extents aliases sizes and ack order")]
    private static void DrainGuards()
    {
        using var f = new Fixture();
        HostCheck.Equal(O.Status.Success, Path(f));
        byte[] original = f.Snapshot();
        O.Header header = default;
        byte[] output = Enumerable.Repeat((byte)0xcc, 8192).ToArray();
        HostCheck.Equal(O.Status.InvalidStorage, O.CopyNext(f.State, Epoch, &header, 143, output));
        HostCheck.Equal(O.Status.InvalidStorage, O.CopyNext(f.State, Epoch, &header, 144, new byte[1]));
        HostCheck.Equal(O.Status.InvalidStorage, O.CopyNext(f.State, Epoch, (O.Header*)f.State, 144, output));
        HostCheck.Equal(O.Status.InvalidStorage, O.CopyNext(f.State, Epoch, &header, 144, new Span<byte>((byte*)f.State - 1, sizeof(O.State) + 2)));
        HostCheck.Equal(O.Status.InvalidStorage, O.CopyNext(f.State, Epoch, &header, 144, new Span<byte>(&header, sizeof(O.Header))));
        HostCheck.Equal(O.Status.StaleEpoch, O.CopyNext(f.State, Epoch + 1, &header, 144, output));
        HostCheck.True(output.All(b => b == 0xcc) && header.Sequence == 0);
        f.Same(original);
        var a = Peek(f, out var bytes);
        var b = Peek(f, out var again);
        HostCheck.True(a.Sequence == b.Sequence && bytes.SequenceEqual(again));
        f.Same(original);
        HostCheck.Equal(O.Status.InvalidRecord, O.Acknowledge(f.State, Epoch, a.Sequence + 1));
        f.Same(original);
        HostCheck.Equal(O.Status.Success, O.Acknowledge(f.State, Epoch, a.Sequence));
        HostCheck.Equal(O.Status.Empty, O.CopyNext(f.State, Epoch, &header, 144, output));
        HostCheck.Equal(O.Status.Empty, O.Acknowledge(f.State, Epoch, a.Sequence));
        HostCheck.Equal(O.Status.Success, Path(f));
        b = Peek(f, out _);
        HostCheck.True(b.Sequence > a.Sequence && b.PayloadOffset == 0);
        Drain(f);
    }

    [HostTest("writable syscall observation original self epoch retirement and sequence exhaustion preserve ownership")]
    private static void Lifetime()
    {
        using var f = new Fixture();
        ulong self = Self(ref *f.State);
        O.Admission admission = Input(ref *f.State);
        Self(ref *f.State) = self + 8;
        byte[] badSelf = f.Snapshot();
        HostCheck.Equal(O.Status.StaleEpoch, Path(f));
        f.Same(badSelf);
        Self(ref *f.State) = self;
        Input(ref *f.State).ObservationEpoch++;
        byte[] badEpoch = f.Snapshot();
        HostCheck.Equal(O.Status.StaleEpoch, Path(f));
        f.Same(badEpoch);
        Input(ref *f.State) = admission;
        Sequence(ref *f.State) = ulong.MaxValue;
        HostCheck.Equal(O.Status.SequenceExhausted, Path(f));
        HostCheck.True(f.State->EvidenceLost && f.State->PendingRecords == 0 && f.State->FailureReason == O.EvidenceFailure.Sequence);
        HostCheck.Equal(O.Status.Incomplete, Path(f));
        HostCheck.Equal(O.Status.Success, O.Stop(f.State, Epoch));
        byte[] retired = f.Snapshot();
        HostCheck.Equal(O.Status.StaleEpoch, Path(f));
        HostCheck.Equal(O.Status.InvalidStorage, O.Start(f.State, Admission));
        f.Same(retired);
    }
}
