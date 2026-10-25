using System;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Optional copied syscall evidence, owned by one serialized supervisor.
/// The caller retains this entire disjoint POD area until drained and stopped.
/// Capture never calls back, reads user memory, allocates, alters a syscall
/// result or grants process, mapping, request, device or persistence authority.
/// Native attachment, source authenticity and sampling every boundary remain
/// separate outer-owner obligations.</summary>
public static unsafe class LinuxWritableSyscallObservation
{
    public const int RecordCapacity = 2, PayloadCapacity = 8192, PathCapacity = 256, StatBytes = 144;
    public const uint MaximumRequestedRead = 65536;
    private const ulong UserEnd = 0x1000000000, SupervisorEnd = 0x0000800000000000;
    public enum Status : byte { Success, Empty, NotRecorded, InvalidStorage, InvalidRecord, StaleEpoch, Busy, Incomplete, CapacityExceeded, SequenceExhausted }
    public enum Kind : byte { Path = 1, PwriteInput, ReadOutput, StatOutput }
    public enum Boundary : byte { Admission = 1, ImmediateOutput, CompletionOutput }
    public enum CopyDisposition : byte { Uncommitted, Committed, Failed, Cancelled, Revoked, Repark }
    public enum EvidenceFailure : byte { None, Capacity, Sequence }

    /// <summary>Copied original owner identities. Numeric values do not admit
    /// their pointers or establish that any observed call actually occurred.</summary>
    public struct Admission
    {
        public ulong DescriptorOwner, AttachmentOwner, AttachmentEpoch, ObservationEpoch;
    }

    public struct CallIdentity
    {
        public ulong DescriptorOwner, AttachmentOwner, AttachmentEpoch, ThreadIdentity, Ordinal, Number, Rip;
    }

    /// <summary>Allzero explicitly means no request existed at capture time.
    /// Observation sequence must never be substituted for this generation.</summary>
    public struct RequestIdentity { public ulong Owner, Epoch, Generation; }

    /// <summary>Copied record header. Address/base values are never dereferenced.
    /// Input records prove only copied bytes, not mutation success. Output records
    /// require the caller's actual committed-copy boundary; the carrier cannot
    /// authenticate that caller fact. PayloadOffset is local batch storage.</summary>
    public struct Header
    {
        public ulong Sequence;
        public CallIdentity Call;
        public RequestIdentity Request;
        public ulong UserAddress, VectorBase, RequestedBytes, FileOffset, ValueBits;
        public uint PayloadOffset, PayloadBytes, OutputPrefix;
        public Kind Kind;
        public Boundary Boundary;
        internal ushort Reserved;
    }

    /// <summary>Noncopyable-by-contract fixed batch. No external buffer pointer
    /// or Span is retained. Two records share8192 append-only bytes; an ACK does
    /// not recycle storage until the whole batch drains. Evidence loss persists
    /// through drain/Stop. These properties are observations, not permissions
    /// to access backing after its enclosing owner releases it.</summary>
    public struct State
    {
        internal ulong Self;
        internal Admission Input;
        internal ulong Sequence;
        internal uint Count, Head, Used;
        internal byte Phase;
        internal EvidenceFailure Failure;
        internal ushort Reserved;
        internal Header First, Second;
        internal fixed byte Data[PayloadCapacity];
        public readonly bool Active => Phase == 1;
        public readonly bool Retired => Phase == 2;
        public readonly bool EvidenceLost => Failure != EvidenceFailure.None;
        public readonly EvidenceFailure FailureReason => Failure;
        public readonly uint PendingRecords => Count - Head;
    }

    /// <summary>Initialize only a complete zeroed, aligned ordinary-RAM area.
    /// The outer owner must first prove every physical/virtual alias exclusion
    /// against F/W/C/B, user backing, DMA/MMIO and callback carriers.</summary>
    public static Status Start(State* s, Admission admission)
    {
        if (!Storage((ulong)s, (ulong)sizeof(State), 8)) return Status.InvalidStorage;
        if (!Owner(admission.DescriptorOwner) || !Owner(admission.AttachmentOwner) ||
            admission.AttachmentEpoch == 0 || admission.ObservationEpoch == 0) return Status.InvalidRecord;
        for (uint i = 0; i < sizeof(State); i++) if (((byte*)s)[i] != 0) return Status.InvalidStorage;
        s->Self = (ulong)s; s->Input = admission; s->Reserved = 0; s->Phase = 1; return Status.Success;
    }

    /// <summary>Pure structural lifetime/nonoverlap query only. It performs no
    /// original-owner, physical mapping, process or device authentication.</summary>
    public static bool CanRetainStorage(State* s, ulong epoch, ulong address, ulong bytes) =>
        Bound(s, epoch) == Status.Success && Storage(address, bytes, 1) && !Overlap(address, bytes, (ulong)s, (ulong)sizeof(State));

    /// <summary>Copy the actual successful PATH read, including its final NUL,
    /// before its local buffer expires. A pre-admission request is allzero;
    /// later BEGIN is joined by complete call identity, never a predicted token.</summary>
    public static Status CapturePath(State* s, ulong epoch, CallIdentity call, RequestIdentity request,
        ulong address, ReadOnlySpan<byte> copiedWithNul)
    {
        Status status = Validate(s, epoch, call, request); if (status != Status.Success) return status;
        if (call.Number is not (2 or 4 or 6 or 87 or 257) || copiedWithNul.Length is < 2 or > PathCapacity ||
            !UserRange(address, (ulong)copiedWithNul.Length)) return Status.InvalidRecord;
        if (!Source(s, copiedWithNul)) return Status.InvalidStorage;
        if (copiedWithNul[copiedWithNul.Length - 1] != 0) return Status.InvalidRecord;
        for (int i = 0; i < copiedWithNul.Length - 1; i++) if (copiedWithNul[i] == 0) return Status.InvalidRecord;
        return Append(s, new() { Call = call, Request = request, UserAddress = address, RequestedBytes = PathCapacity,
            Kind = Kind.Path, Boundary = Boundary.Admission }, copiedWithNul);
    }

    /// <summary>Copy one already-copied iovec value and its supervisor scratch
    /// bytes before provider admission and scratch clearing. Zero length retains
    /// a metadata-only record and never dereferences the copied base address.</summary>
    public static Status CapturePwrite(State* s, ulong epoch, CallIdentity call, RequestIdentity request,
        ulong iovecAddress, ulong copiedBase, ulong copiedLength, ulong offset, ReadOnlySpan<byte> copiedPayload)
    {
        Status status = Validate(s, epoch, call, request); if (status != Status.Success) return status;
        if (call.Number != 328 || !UserRange(iovecAddress, 16) || copiedLength > PayloadCapacity ||
            copiedLength != (ulong)copiedPayload.Length || offset > long.MaxValue ||
            copiedLength != 0 && !UserRange(copiedBase, copiedLength)) return Status.InvalidRecord;
        if (!Source(s, copiedPayload)) return Status.InvalidStorage;
        return Append(s, new() { Call = call, Request = request, UserAddress = iovecAddress, VectorBase = copiedBase,
            RequestedBytes = copiedLength, FileOffset = offset, Kind = Kind.PwriteInput, Boundary = Boundary.Admission }, copiedPayload);
    }

    /// <summary>Record positive read bytes only after actual Output success,
    /// before clearing scratch. Failed, cancelled, revoked and reparking paths
    /// publish nothing. EOF has no copied-byte record. The caller retains the
    /// original fd/thread/mapping/request guards; no copy is attempted here.</summary>
    public static Status CaptureRead(State* s, ulong epoch, CallIdentity call, RequestIdentity request, Boundary boundary,
        CopyDisposition disposition, ulong destination, ulong requested, uint outputPrefix, long result, ReadOnlySpan<byte> committed)
    {
        Status status = Validate(s, epoch, call, request); if (status != Status.Success) return status;
        if (!OutputBoundary(boundary, request) || call.Number is not (0 or 17) ||
            disposition > CopyDisposition.Repark) return Status.InvalidRecord;
        if (disposition != CopyDisposition.Committed) return Status.NotRecorded;
        if (result == 0 && committed.Length == 0) return Status.NotRecorded;
        if (result <= 0 || result > PayloadCapacity || result != committed.Length || requested > MaximumRequestedRead ||
            (ulong)result > requested || outputPrefix > PayloadCapacity || (ulong)result > outputPrefix ||
            !UserRange(destination, (ulong)result)) return Status.InvalidRecord;
        if (!Source(s, committed)) return Status.InvalidStorage;
        return Append(s, new() { Call = call, Request = request, UserAddress = destination, RequestedBytes = requested,
            ValueBits = (ulong)result, OutputPrefix = outputPrefix, Kind = Kind.ReadOutput, Boundary = boundary }, committed);
    }

    /// <summary>Record the exact successful Linux x64 stat wire after Output,
    /// including immediate results when no F token genuinely existed. Missing
    /// token is invalid for CompletionOutput. This is copied wire, not a new
    /// metadata, inode, mapping or cursor authority.</summary>
    public static Status CaptureStat(State* s, ulong epoch, CallIdentity call, RequestIdentity request, Boundary boundary,
        CopyDisposition disposition, ulong destination, uint outputPrefix, long result, ReadOnlySpan<byte> committed)
    {
        Status status = Validate(s, epoch, call, request); if (status != Status.Success) return status;
        if (!OutputBoundary(boundary, request) || call.Number is not (4 or 5 or 6) ||
            disposition > CopyDisposition.Repark) return Status.InvalidRecord;
        if (disposition != CopyDisposition.Committed) return Status.NotRecorded;
        if (result != 0 || committed.Length != StatBytes || outputPrefix is < StatBytes or > PayloadCapacity ||
            !UserRange(destination, StatBytes)) return Status.InvalidRecord;
        if (!Source(s, committed)) return Status.InvalidStorage;
        return Append(s, new() { Call = call, Request = request, UserAddress = destination, RequestedBytes = StatBytes,
            OutputPrefix = outputPrefix, Kind = Kind.StatOutput, Boundary = boundary }, committed);
    }

    /// <summary>Peek the original head without consuming it. All complete output
    /// extents and mutual aliases are validated before any write. Failure leaves
    /// caller bytes and the batch untouched; no large temporary stack is used.</summary>
    public static Status CopyNext(State* s, ulong epoch, Header* header, uint headerBytes, Span<byte> payload)
    {
        Status status = Bound(s, epoch); if (status != Status.Success) return status;
        if (headerBytes != sizeof(Header) || !Outside(s, (ulong)header, (ulong)sizeof(Header))) return Status.InvalidStorage;
        fixed (byte* target = payload)
        {
            if (payload.Length != 0 && (!Outside(s, (ulong)target, (ulong)payload.Length) ||
                Overlap((ulong)header, (ulong)sizeof(Header), (ulong)target, (ulong)payload.Length))) return Status.InvalidStorage;
            if (s->Head == s->Count) return Status.Empty;
            Header next = s->Head == 0 ? s->First : s->Second;
            if ((uint)payload.Length < next.PayloadBytes) return Status.InvalidStorage;
            for (uint i = 0; i < next.PayloadBytes; i++) target[i] = s->Data[next.PayloadOffset + i];
            *header = next; return Status.Success;
        }
    }

    /// <summary>ACK only the exact copied head sequence after external emission
    /// succeeded. Scrub its payload; recycle the arena only when both records
    /// drain. A failed external serializer must leave this record retained.</summary>
    public static Status Acknowledge(State* s, ulong epoch, ulong sequence)
    {
        Status status = Bound(s, epoch); if (status != Status.Success) return status;
        if (s->Head == s->Count) return Status.Empty;
        Header* head = s->Head == 0 ? &s->First : &s->Second;
        if (sequence == 0 || head->Sequence != sequence) return Status.InvalidRecord;
        for (uint i = 0; i < head->PayloadBytes; i++) s->Data[head->PayloadOffset + i] = 0;
        *head = default; s->Head++;
        if (s->Head == s->Count) { s->Head = s->Count = s->Used = 0; }
        return Status.Success;
    }

    /// <summary>Retire only a drained carrier. Scrub bytes and retain historical
    /// failure/self/epoch facts. This neither frees backing nor authorizes reuse;
    /// the outer owner must retain it through all outstanding cleanup.</summary>
    public static Status Stop(State* s, ulong epoch)
    {
        Status status = Bound(s, epoch); if (status != Status.Success) return status;
        if (s->Head != s->Count) return Status.Busy;
        for (int i = 0; i < PayloadCapacity; i++) s->Data[i] = 0;
        s->First = s->Second = default; s->Phase = 2; return Status.Success;
    }

    private static Status Validate(State* s, ulong epoch, CallIdentity call, RequestIdentity request)
    {
        Status status = Bound(s, epoch); if (status != Status.Success) return status;
        if (!Call(s, call) || !Request(s, request)) return Status.InvalidRecord;
        return Status.Success;
    }
    private static bool Call(State* s, CallIdentity c) => c.DescriptorOwner == s->Input.DescriptorOwner &&
        c.AttachmentOwner == s->Input.AttachmentOwner && c.AttachmentEpoch == s->Input.AttachmentEpoch &&
        (c.ThreadIdentity >> 32) != 0 && (uint)c.ThreadIdentity is >= 1 and <= 16 && c.Ordinal is >= 1 and <= 4096 &&
        c.Rip >= 4096 && c.Rip < UserEnd;
    private static bool Request(State* s, RequestIdentity r) => r.Owner == 0 && r.Epoch == 0 && r.Generation == 0 ||
        r.Owner == s->Input.AttachmentOwner && r.Epoch == s->Input.AttachmentEpoch && r.Generation != 0;
    private static bool OutputBoundary(Boundary b, RequestIdentity r) => b == Boundary.ImmediateOutput ||
        b == Boundary.CompletionOutput && r.Generation != 0;
    private static Status Append(State* s, Header header, ReadOnlySpan<byte> bytes)
    {
        if (s->Failure != EvidenceFailure.None) return Status.Incomplete;
        if (s->Sequence == ulong.MaxValue) { s->Failure = EvidenceFailure.Sequence; return Status.SequenceExhausted; }
        if (s->Count == RecordCapacity || (uint)bytes.Length > PayloadCapacity - s->Used)
        { s->Failure = EvidenceFailure.Capacity; return Status.CapacityExceeded; }
        header.Sequence = ++s->Sequence; header.PayloadOffset = s->Used; header.PayloadBytes = (uint)bytes.Length;
        for (int i = 0; i < bytes.Length; i++) s->Data[s->Used + (uint)i] = bytes[i];
        if (s->Count == 0) s->First = header; else s->Second = header;
        s->Count++; s->Used += (uint)bytes.Length; return Status.Success;
    }
    private static Status Bound(State* s, ulong epoch)
    {
        if (!Storage((ulong)s, (ulong)sizeof(State), 8)) return Status.InvalidStorage;
        if (epoch == 0 || s->Self != (ulong)s || s->Input.ObservationEpoch != epoch || s->Phase != 1) return Status.StaleEpoch;
        if (!Owner(s->Input.DescriptorOwner) || !Owner(s->Input.AttachmentOwner) || s->Input.AttachmentEpoch == 0 ||
            s->Count > RecordCapacity || s->Head > s->Count || s->Used > PayloadCapacity || s->Failure > EvidenceFailure.Sequence ||
            s->Reserved != 0 || s->Count == 0 && (s->Head != 0 || s->Used != 0)) return Status.InvalidStorage;
        ulong previous = 0;
        for (uint i = s->Head; i < s->Count; i++)
        {
            Header r = i == 0 ? s->First : s->Second;
            if (r.Sequence == 0 || r.Sequence <= previous || r.Sequence > s->Sequence || r.PayloadOffset > s->Used ||
                r.PayloadBytes > s->Used - r.PayloadOffset || r.Reserved != 0 || !Call(s, r.Call) || !Request(s, r.Request) ||
                r.Kind is < Kind.Path or > Kind.StatOutput || r.Boundary is < Boundary.Admission or > Boundary.CompletionOutput)
                return Status.InvalidStorage;
            previous = r.Sequence;
        }
        return Status.Success;
    }
    private static bool Source(State* s, ReadOnlySpan<byte> source)
    { fixed (byte* p = source) return source.Length == 0 || Outside(s, (ulong)p, (ulong)source.Length); }
    private static bool Outside(State* s, ulong address, ulong bytes) => Storage(address, bytes, 1) &&
        !Overlap(address, bytes, (ulong)s, (ulong)sizeof(State));
    private static bool Owner(ulong value) => Storage(value, 1, 8);
    private static bool UserRange(ulong start, ulong bytes) => start >= 4096 && bytes != 0 && start < UserEnd && bytes <= UserEnd - start;
    private static bool Storage(ulong start, ulong bytes, ulong alignment) => start >= 4096 && (start & (alignment - 1)) == 0 &&
        bytes != 0 && start < SupervisorEnd && bytes <= SupervisorEnd - start;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => n != 0 && m != 0 && a < b + m && b < a + n;
}
