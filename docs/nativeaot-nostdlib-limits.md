# NativeAOT NoStdLib Limits

[Step1601](../done/step1601.md) The host file-backed PE catalog now permits4096records; this does not expand the kernel unwind decoder MaximumFunctions2048 or its snapshot/lookup/EH contract. An enabled kernel unwind probe still rejects a whole table beyond2048 with CapacityExceeded. The timing disk profile requires everyM04flag off and does not invoke that decoder.

The isolated continuing-disk profile has original ILC/link/artifact verification
and two retained failed diagnostic guests in steps1566/1567. The second reaches
exit/startup/NIC records before memory admission rejects its 65 static mappings.
The profile-specific range repairs need fresh native/guest qualification; full
ABI/stack, retained image/arena and real device progress remain unaccepted.
Its fixed observation records contain
copied POD/sector bytes; callbacks allocate no storage and cannot reenter a
block mutator. Accepted older profiles retain their exact source/native scopes.

## Runtime expansion and toolchain maintenance

Keep kernel C# restricted to the runtime behavior actually needed by supported
OS operations. Before expanding kernel GC, EH, initialization, metadata or helper
support, record the product requirement and why bounded storage, explicit
ownership, status returns or a simpler code shape cannot satisfy it. A new
compiler helper emitted by convenient syntax is a reason to inspect that syntax,
not automatic authorization to implement more of the .NET runtime. Existing
bounded GC/EH probes retain their exact scopes; this policy does not remove them
or waive evidence for code that depends on them.

Guest applications retain their pinned upstream NativeAOT runtime, including GC,
EH and BCL behavior. Supply the measured OS services beneath the application ABI;
do not share kernel GC roots, heaps, exception metadata or unwind ownership with
the guest runtime. Future PE hosting reuses OS services but requires a separately
qualified application/runtime boundary.

When changing compiler/runtime pins or a native boundary, automate detection of
the affected layouts, helpers, relocations, calling conventions and unwind
assumptions in host tooling. Keep exact input identities and independently
authored positive/negative controls. Report unsupported output explicitly; do not
silently widen the contract to make verification pass. Native review may diagnose
a new compiler behavior, but normal supported application publication must not
require per-app manual disassembly, a reference-VM analysis session or edits to
kernel/verifier allow-lists. Package checksums establish integrity, not a list of
application-specific implementations. The generic publication/admission work in
[the workload roadmap](workload-profiles.md#canonical-successor-issue-keys-2026-10-23)
must replace that deployment dependency before general hosting is claimed.

The user-selected .NET 11 migration is tracked in
[#727](http://192.168.4.35:3000/admin/CLROS/issues/727). The active compiler
source is dotnet/dotnet `820230c386db5b0106c5430b3e5aee92a7826bf5`.
Its RTR26.1 module rows contain section ID, signed length and start pointer
(16 bytes), replacing the old flags/start/end form. Interface dispatch also
has a new metadata/cell ABI. The bounded adapters and finite profile matrix are
qualified in [step1225](../done/step1225.md); historical .NET 10 sections below
do not establish support for additional .NET 11 profiles.
Object/array/string, GC descriptors, class constructors, EH and CPU-transfer
contracts must each retain their independently reviewed evidence boundary.
Current commands and version policy are in [host requirements](host-requirements.md).
The same document records the C# 15 research: managed unions and preview
memory-safety features are not implicit additions to the no-stdlib surface.

M16-002 adds `OS.Std.Hash.Sha256.TryCompute`: allocation-free,
one-shot SHA-256 over trusted caller pointers, at most 128 MiB, with 32-byte caller
output. Invalid/null nonempty input, small output, overflow and overlap fail
before writing. Padding/standard vectors and independent .NET comparisons pass.
That qualified profile supplies no incremental hashing, file/stream overloads,
HMAC/signature API or certification. Kernel use is the 16 MiB data-only Linux ELF staging profile; it does
not initialize upstream app GC/EH or a hosted runtime. See
[step1199](../done/step1199.md) for qualified profile evidence.

[Step1421](../done/step1421.md) introduces `HmacSha256.TryCompute`
(keys0..64, messages0..256, disjoint32-byte output) and `HmacSha256Drbg`
(`Instantiate`, `Generate`, `Reseed`, `Erase`). The serialized, address-bound
owner requires32 entropy+16 nonce bytes, optional0..32-byte personalization,
1..8192-byte requests and explicit fresh reseed after2^48 requests. It follows
[SP800-90A rev1 section10.1.2](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-90Ar1.pdf).
Three original NIST known answers and independent .NET HMAC comparisons pass.
Invalid admission preserves storage; addressed SHA/HMAC scratch and owner state
are erased using `SensitiveMemory.Clear`, with inlining/optimization disabled.
`IsInitialized` validates the exact owner address/counter without output; a
reseed-required owner remains initialized but cannot generate. [Step1423](../done/step1423.md)
adds explicit service source/adapter/cleanup wiring, with guarded independent
firmware entropy/nonce capture and no firmware callbacks after exit. Native
erase and bounded service ownership evidence follow in step1437, with corrected
full-host qualification in step1481 and retained regressions in step1556.
Registers/other copies, entropy quality, prediction resistance, longer
HMAC keys/messages, streams and certification are outside this API's guarantees.

#674–#676 qualify one default-off `EnableM04NormalFinallyProbe` Diagnostic in
[step1195](../done/step1195.md) at frozen source `6acb593`: one compiler-called
finally throws the original rooted exception, own-funclet unwind recovers the
same-root parent, and the outer typed catch reaches the original compiled
continuation. Already exited/earlier clauses are skipped in both dispatch passes.
The live parent-local witness, exactly-one counters, full required GP/XMM and
Win64 control state, seven stack checks and original canaries pass. The explicit
root remains retained through continuation; expired parent locals are not read.

4,546 full-host checks across 592 groups (94 Native) have a complete/successful/
unchanged-input receipt, zero failures/timeouts/reuse and terminal
`TEST_PASS host.tests`. Current review covers 170 callback nodes, seven indirect
sites, 13 bounded body allocations and 57 targets in five readonly switch tables;
the first-party bound is 14,592/16,400 bytes, excluding firmware internals.
Three real 88-record guests, nine affected ancestors, 61 native identity/ABI
controls and 339 synthetic transcript controls pass. Each capture binds all 693
raw inputs and original compiler/catalog/link/loaded-PC/process/argument/GPT/FAT/
embedded-EFI evidence. Firmware services remain active in this Diagnostic.
General EH, collided unwind, arbitrary methods, dynamic module lifetimes, #90/Gate4, hosted .NET/Kestrel, cloud and production remain incomplete.

[step1190](../done/step1190.md) records the initial inactive compiler fixture:
ILC10.0.12 emits a normal-path call to its own finally
funclet when the root has `MethodImpl` NoOptimization (`0x40`). Without that flag,
the compiler clones the normal finally into the root. Original COFF/map and
linked PE review establish 173 code bytes, 11 EH bytes, three frame records and
the same-root catch continuation. The catalog producer can select one exact
method after validating every original contribution, including omitted ones.
The temporary frame-profile anchor has been removed. Its measurements remain
development evidence; final qualification is recorded above.

#670–#672 qualified the earlier host prerequisites for a normal-path-finally fixture
in [step1189](../done/step1189.md): 4,539 full-host checks across 588 groups (92
Native), unchanged inputs, native callback/stack review and four affected guest
regressions. A plain handler funclet's own unwind restores the same-root parent
GP/XMM context, preserves actual RIP and identifies the exact exited clause from
complete original catalog/EH metadata. The existing planner skips that clause and
earlier clauses in both passes while validating every row and typed receipt.
The encoded kind cannot distinguish fault from finally or prove invocation
history. At that prerequisite-only snapshot parent recovery was outside EFI and
the skip entry had no native caller. Existing catalog/nested/frame/unhandled captures are regression evidence
only for that prerequisite batch. It proved no normal-finally CPU transfer;
step1195 qualifies the separate fixture above. #90/Gate4 remain incomplete.

`BoundedExceptionCallerWindow` is qualified in [step1187](../done/step1187.md): 4532 full-host checks across 587 groups (92 Native),
original native review, three 95-record guests and twelve ancestors. It assembles
up to four physical contexts and sixteen decoded rows without allocation. Actual
RIP stays intact; RIP minus one selects protected membership. Exact EH lengths
come from the compiler catalog; zero-row frames require an authenticated no-EH
root. Late failure clears admitted outputs. Image-resident scratch needs full
MutableData admission and separation from retained owners, snapshot and readable
stack. No roots, lifetime or CPU authority are acquired. General EH, arbitrary methods, dynamic module lifetimes, collided unwind, #90/Gate4, hosted .NET/Kestrel, cloud and production remain incomplete.

The #654/#655/#656 filtered-catch batch is qualified in [step1181](../done/step1181.md):
4518 full-host checks across 582 groups (90 Native), original native review, three byte-identical
88-record guests and eleven ancestors pass at frozen source `79b59b2`. A separate
four-row POD search validates the complete same-range table and ordered receipt
prefix; zero is false and signed nonzero is true. The helper owns no invocation,
root, module or stack authority. A returning 328-byte wrapper preserves selector
GP/full XMM6-15/FP controls and invokes an authenticated nonthrowing compiler filter
under the original parent context. Actual false then true filters share one
parent local, then only the second catch and compiled continuation execute.
Existing typed/cleanup planners still reject filters. Filter throws, nested filters, collided unwind, arbitrary EH, cached constructor rethrow/repeated compiler access, #90/#91/#84, Gate4, hosted .NET/Kestrel, cloud and production remain incomplete.
Steps1177-1180 retain candidate, offset-guard and parser-count development history;
the final step also preserves the cancelled input-change full run.

The qualified ARP batch in [step1164](../done/step1164.md) extends the shared
pointer-backed unmanaged Span subset with bounds-checked indexing, Slice,
Clear and conversion to a pointer-backed ReadOnlySpan. The read-only view
supports Length, indexing, Slice and pinning. Host mirrors throw for invalid
bounds; native invalid bounds use the existing fail-fast halt. Caller-owned
mapping and lifetime remain required. Arrays, strings, enumeration, general
copy/marshalling and moving-GC interior references are unsupported. The empty
`InAttribute` supplies the compiler identity for ref-readonly metadata only.
The linked ARP paths pass native ABI/stack review, a real 194-record guest
capture, 4,441 full-host checks and thirteen ancestor regressions. Its diagnostic
bound is 12,760 bytes. Current block ancestors conservatively bound diagnostics
at 30,456 bytes, including the shared negotiation frame growth; all fit the
owned 32KiB stack. This does not qualify a general Span/BCL or runtime surface.

[Step1320](../done/step1320.md) additionally admits `Span<T>.Empty` and
`ReadOnlySpan<T>.Empty` as default zero-length views with no backing storage,
allocation or exception. Empty pinning returns null; no additional overloads or
general BCL behavior are supplied. The exact TCP source closure now compiles in
the M07-026 managed no-stdlib profile using the already-admitted overlap-safe
`BufferOperations.MemoryCopy`. [Step1332](../done/step1332.md) adds original native
review,5,341 full-host checks, three strict TCP guests and20 affected ancestors.
The finite profile retains queues and a guarded cooperative worker through real
TCP/240-second TIME-WAIT and checked cleanup. It adds no general BCL, application
socket/epoll, kernel EH or guest Kestrel support. Firmware reservations stay retained.

[Step1162](../done/step1162.md) accepts the four explicit modern-net EFI compile
inputs and block MSI-X/recovery integration with strict source/pointer admission.
Linked native POD ABI, unwind, dynamic scratch and complete callbacks are reviewed:
block diagnostic bound 30,440, transient 22,000 and net diagnostic 11,720 bytes remain
within the owned 32KiB stack. Graph aids alone confer no acceptance. This finite
device qualification does not expand general runtime or hosted exception support.

[Step1153](../done/step1153.md) adds host-tested shared pointer ASCII matching
(packed tokens up to 24 bytes), bounded ASCII relative paths, and exact
512-byte file-sector copying into caller-owned memory. These use pointer-backed
Span's Length/fixed surface, reject null/wrapping numeric ranges and aliases
(including the copy counter), allocate no memory and return expected failures.
Caller-owned mapped/readable/writable backing is still required. New bounded
manifest/package/staging helpers use that surface and became explicit EFI
compile items in step1154. [Step1156](../done/step1156.md) admits their linked
native bytes only for the bounded package profile, with full-host/native/guest
review. Caller backing requirements and the broader runtime compatibility
ledger's separate evidence boundaries remain unchanged.

The qualified FAT slice in [step1152](../done/step1152.md) uses existing pointer-backed
`Span<byte>`, with `Span<uint>` and `Span<ushort>` specializations for fixed caller
scratch. A shared pointer `ReadUInt16LittleEndian(byte*)` overload reads exactly
two readable bytes, including unaligned input, with no allocation or exception.
The caller owns pointer validity. No framework Span indexing/slicing, strings,
collation or runtime-surface expansion is added. Associated native instructions,
bounded stack, real guest capture and full-host checks qualify this fixed-scratch
profile; they do not admit general Span, directory service or filesystem behavior.

The GPT batch [step1150](../done/step1150.md) accepts three explicit storage sources
using the existing pointer-backed Span and shared endian/CRC helpers. It introduces
no new BCL surface. The guest owns one fixed 16KiB entry buffer, a 512-byte header
and bounded repeated fresh read calls. The closed constrained specialization,
hidden return buffers and caller ownership are reviewed. Conservative diagnostic/
fault stack bounds31736/4968 bytes fit the 32,768-byte stacks, including the fixed
array's page-probe loop and machine-stub margins. These bounds apply only to the
captured profile; no general Span/interface/BCL support is established.

## Current Status

The #122/#123/#625 batch accepted in [step1147](../done/step1147.md) adds a narrow pointer-backed `System.Span<T>`
surface from `std/no-runtime/shared/Memory/SystemSpanPointer.cs`: pointer/length
constructor, `Length` and `GetPinnableReference`, restricted to unmanaged T.
The same storage operations build as an `OS.Std.NoRuntime.Span<T>` host mirror.
Default/zero-length pinning yields a null reference; nonempty pinning retains
the supplied address. Construction/pinning allocates nothing and does not map,
validate or retain the pointed-to memory; the caller owns its full lifetime.
Negative lengths throw in the host mirror and halt in the no-stdlib surface.
Reference-containing T, arrays, indexing, slices, enumeration and copying are
not supplied. This is not general Span/BCL or moving-GC interior-reference support.

Source basis is the pinned runtime's
[Span layout and pointer/pinning contracts](https://github.com/dotnet/dotnet/blob/95017c711e6afc1085133d440e42b4bd78155701/src/runtime/src/libraries/System.Private.CoreLib/src/System/Span.cs)
and [ByRefFields compiler feature identity](https://github.com/dotnet/dotnet/blob/95017c711e6afc1085133d440e42b4bd78155701/src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.cs).
Concrete compilation/publish diagnostics require the unmanaged-constraint
`UnmanagedType` identity (no marshalling values/API), `RuntimeFeature.ByRefFields`
and `Int32.MaxValue`. Host parity, strict source admission, full-host and real
guest qualification pass. Targeted native review verifies the exercised closed
constrained Info/Read calls, pointer/length layout and aggregate returns without
boxing or unsupported helpers. The reviewed diagnostic stack bound is 9528
bytes and the separate fault bound is 4968 bytes, within 32768-byte stacks.
Constant empty/default pinning controls may fold; this is not arbitrary Span
lowering, interface-reference dispatch or general GC support.

`OS.Std.Memory.MemoryOrder.Fence(void*, delegate* unmanaged<void*, byte>)` is
an OS-specific allocation-free status API, not `System.Threading.Thread` or BCL
compatibility. It rejects a null callback, invokes a non-null callback exactly
once, and reports success only for return 1. Context may be null. Failed calls
may have effects; no exception handling, callback authentication, mapping or
lifetime validation is supplied by this utility. The adapter owns those duties.
There are no other overloads. The bounded x64 queue adapter authenticates and
executes its fixed MFENCE leaf; host callback/byte tests alone do not prove
hardware ordering. See [drivers](drivers.md) and [step1133](../done/step1133.md).

`OS.Std.Binary.EndianPrimitives` now has pointer overloads for 32-bit and
64-bit little-endian reads. They use unaligned-safe byte loads without allocation
or exceptions; the caller must retain readable four/eight-byte storage. Only
the pointer partial is linked into the EFI closure. Independent byte vectors
pass. This adds no general BCL or pointer-lifetime guarantee; see [step1122](../done/step1122.md).

The default-off `EnableM04RethrowProbe` implements one bounded compiler-generated
rethrow: original throw, one finally cleanup, inner typed catch, rethrow of the
same object, outer typed catch and compiled continuation. Two generated bridges
retain the reviewed parent-frame, stack and register contract from step1100.
The C# publication/verifier and 88-record guest path pass in
[step1120](../done/step1120.md), including full-host and image qualification. Filters,
collided exceptions, arbitrary nested rethrow and general EH remain unsupported.

The accepted #90 batch (#638-#640) has a bounded one-method dispatch policy:
at most four decoded physical clauses, typed catch search and two strictly
nested exceptional cleanup targets. It reuses the admitted metadata reader and
retained-object type matcher; no partial successful plan survives an invalid
tail. Filters, group markers and ambiguous/reversed/crossing ranges are rejected.
A separate two-cleanup bridge passes instruction/ABI controls while existing
catch/finally/rethrow object identities remain unchanged.

The separate default-off `EnableM04FrameDispatchProbe` implements one returning
finally from a strictly earlier physical frame and an outer typed catch, using
different nonvolatile GP/XMM contexts and the actual catch-produced continuation.
It retains the original rooted object and registered module throughout transfer,
with registered mutable authority for its fixed cleanup scratch. The finite
profile is qualified in [step1171](../done/step1171.md): 4,488 full-host checks,
final native review, three 88-record frame guests and seven ancestors. Step1170
retains development geometry and callback review. General second-pass unwinding,
filters, collided exceptions and arbitrary rethrow remain incomplete.

The default-off `EnableM04EhCatalogProbe` modifies that same finite profile.
Exact original ILC code/EH lengths and relocated root/table RVAs replace its
per-method bounds getters in clause admission. Lookup validates every catalog
row, original EH stream and funclet association before publishing a match.
The native source/relocation, 21 hostile controls and 177-node callback review
pass in [step1183](../done/step1183.md), with a 15,856-byte upper bound below the
original 16,400-byte floor. Full-host and guest qualification remain pending.

The default-off `EnableM04ConstructorFailureProbe` is qualified
in [step1176](../done/step1176.md). Its 40-byte POD invocation ticket
retains the existing 24-byte record: failure pops the active record and preserves
the nonzero compiler constructor word. The diagnostic has a real compiled throw,
wrapping catch and outer `TypeInitializationException` catch, original and wrapper
allocation roots, one checked inner-reference store, and two physical bridge
lifetimes. The bounded wrapper exposes the original inner object and constructor
context; type-name, message, stack-trace and general cached-failure services are
absent. Native review, 16 PE and one object mutation controls, 4,502 full-host
checks, three 92-record guests and ten ancestors pass. The raw Read context is
retained while a local copy interprets only its pinned body branch; generic
unwind branch admission is unchanged. A denied BeginLazy
operation is not evidence of a second real compiler access or cached rethrow.

The separate default-off `EnableM04UnhandledExceptionProbe` has measured
10/46/56-byte thrower/middle/catcher contributions, a nonmatching sibling clause
and a profile-only real type-test export. Original context/module/root authority
remains mandatory. It terminates the first pass at three bounded frames without
second-pass cleanup. [Step1169](../done/step1169.md) qualifies 4,473 full-host
checks, native review, three 73-record expected-terminal guests and six
ancestors with source/process/image association. General multi-frame dispatch,
general EH and constructor cached failures remain incomplete; step1168 retains
development evidence.

`EnableM04NestedFinallyCatchProbe` now links a measured 222-byte method, four
clauses and five unwind rows. Three 89-record guests complete sibling nonmatch,
base match, two cleanups, catch and compiled continuation. The 4,456-check full
gate, native controls and five ancestor regressions qualify the finite batch in
[step1167](../done/step1167.md). Its annotation/digest follow-up passes six focused
checks and three fresh guests with identical EFI bytes; step1166 retains failures.
Module/root, stack, compiler-contribution and transfer authority remain caller
obligations. [Step1165](../done/step1165.md) records the host leaves; #90 and
Gate4 remain incomplete. General nested exceptions, filters and collided unwind
remain unsupported.

CLROS implements a bounded no-stdlib NativeAOT subset for the EFI shell and the
optional allocation, collection, metadata and constructor probes described
below. `std/no-runtime/shared` contains compatible helper slices; it is not a
complete BCL or general runtime surface.

The historical compiler/runtime profile was qualified at `10.0.12` on SDK
`10.0.401` for the finite host/native/guest matrix recorded in step1078.
See [the current EFI toolchain](efi-build.md#current-toolchain-migration) and
[step1078](../done/step1078.md) for its qualification boundary. Historical
`10.0.8` acceptance below remains tied to its original source and artifacts.

## Accepted bounded interface dispatch

This section records the earlier .NET 10 acceptance. The .NET 11 candidate
uses zero-initialized two-word cells in section210, NativeArray metadata in
section209 and relative NativeReferences in section331. Its 814-byte bridge
exports `RhpInterfaceDispatch`; instruction bytes and relocations match the
independently checked earlier bridge. Compiler cells remain unchanged and the
bounded cache remains external. Original new COFF and guest qualification
belong to #727; the old stub/cache-word and run-terminator layout below is not
admitted as .NET 11 input.

M04-016 uses the original registered module and current allocator receipt to
admit a receiver before reading its method table. The core consumes the pinned
compiler's two-word cells with relative interface references; it does not invent
a ReadyToRun section ID for those cells. Cells remain unchanged. Caller-owned
POD storage holds metadata snapshots and a finite cache keyed by cell, receiver
and current allocation sequence. Cached calls recheck the original authorities.

Resolution supports ordinary class implementations, exact interface matching,
class-reference covariance/contravariance, receiver virtual slots and sealed
slots belonging to the declaring class. A sealed table has no in-band count:
the caller must supply the exact compiler contribution extent, including a
folded contribution's identity. An address inside the image is insufficient.
Limits are 64 cells, 64 cache entries, 1,024 metadata words, 16 hierarchy levels
and 16 sealed-table owners. The maximum configured state occupies 32,056 bytes.

The C# COFF generator emits an 828-byte object that bridges Win64 receiver
`RCX` and cell `R11` to the ordinary `CLROS_InterfaceDispatch_Resolve` callback.
It preserves GP/XMM argument state and the original stack arguments, then
tail-jumps to the validated target. A rejected callback returns zero and the
bridge halts locally. The bridge is not an exception/unwind implementation.

Final native/QEMU acceptance at `d5bef46` binds 112 bytes for five cells plus two
terminators and the four-byte base sealed table. All five consumers retain real
R11 indirect calls. The fresh guest passes all 15 calls, ten cache entries and
five cache hits while retaining the original stack, roots and counters.
Current native ABI/stack/caller-storage review, the full 158-record stream and
the same-image null-receiver rejection pass in [step1063](../done/step1063.md).
That step also records the original complete host gate, ancestor/default
regressions and preserved control failure. Dynamic types,
arrays/boxing, generic receiver classes, generic virtual methods, default/static
interface implementations, large struct returns, concurrency and throwing
dispatch failures remain unsupported. This does not complete Gate4.

## Class-constructor implementation order

The earlier ILC 10.0.12 package identifies source revision
[`95017c711e6afc1085133d440e42b4bd78155701`](https://github.com/dotnet/dotnet/tree/95017c711e6afc1085133d440e42b4bd78155701/src/runtime).
Its presence check for `System.Runtime.CompilerServices.ClassConstructorRunner`
enables lazy initialization. The compiler allocates one pointer-sized context
word immediately before the non-GC static base; named managed helpers return
the incoming non-GC or GC base. Eager section 205 instead contains signed
relative-32 entries on win-x64. These are source-derived ABI requirements,
now implemented by the bounded M04-024 core and profile-only compiler adapter.
The original full host gate and fresh native/QEMU constructor capture are
accepted at `2b5a4b0` in [step1053](../done/step1053.md). Same-image review proves
the real eager target, seven lazy contexts, successful-return completion,
original static-base returns and bounded direct/cyclic reentry. Development
history remains in [step1052](../done/step1052.md).

M04-024 separates real eager/lazy execution, once semantics and same-thread
reentry from the full M04-015 failure requirement. After M04-014 it enables
M04-016 dispatch and the existing path through M04-021 exception/unwind work.
M04-015 then completes constructor-specific exception caching and rethrow;
M04-023's final helper audit depends on it as well as M04-022. This removes the
otherwise circular dependency between constructor failure handling and unwinding
without weakening the full constructor milestone.

The precursor must reuse the retained module/GC owner, admit actual compiler
contexts and executable targets, invoke real managed `void` constructors, and
clear the lazy completion word only after successful return. Eager entries use
runner-owned once bookkeeping; they have no lazy context word. In same-thread
`A -> B -> A` recursion, the active A returns its partial static base without
reinvocation or clearing its word. A status-returning callback or blanket
cycle rejection cannot substitute for this behavior. Cross-thread execution
remains a separate prerequisite.

Terminal containment before unwinding is available proves only that the failed
constructor did not complete. Full M04-015 still requires a real throwing
constructor, rooted/cached `TypeInitializationException`, and repeated-access
rethrow without running the constructor again. The existing host state tables
and token plans do not satisfy either guest boundary. Shared EFI/boot/project/
probe/script integration is owned by #570.

`CompilerClassConstructorExecution` revalidates the original retained module
owner and registered ranges on every entry. It snapshots and validates all
eager targets before invocation, tracks once state per table entry (identical
folded targets are allowed), and validates lazy mutable contexts and readable,
executable, nonwritable targets. State lives outside the one-word compiler
context. Active direct or cyclic same-thread access returns the partial static
base without clearing the callable; only successful outer completion clears
it. The EFI adapter uses zero-initialized retained POD storage, the original GC
owner, and runtime-fed compiler fixtures. Host synthetic executable ranges are
validated but never executed. Actual compiler targets, helper callsites and
context/table identities still require same-image native and guest evidence.

The pinned compiler also guards a constructor's accesses to its own static
storage. The fixture therefore observes five depth-one and three depth-two
active helper reentries while all eight constructor bodies execute once.
These counts are derived from the emitted compiler callsites and checked
against the real guest, not interpreted as repeated constructor invocation.
The profile's empty `IsVolatile` metadata type exists only to encode the
compiler-required volatile pointer field; it adds no runtime behavior.

## Bounded exception-type predicate

`ExceptionTypeMatch.Match` is a host-tested prerequisite for M04-021 handler
search, scoped in #575 and [step1058](../done/step1058.md). For a nonnull object,
it validates the original retained allocation and module authority, then admits
both complete class chains before reporting exact/base identity or a nonmatch.
Each chain is limited to 32 immutable same-image 24-byte MethodTable prefixes.
The pinned ILC 10.0.12 accessor uses a direct absolute related-type pointer;
imported, tagged and relative encodings are not resolved. Malformed metadata,
cycles, excessive depth and unsupported types return distinct failure statuses.
Null returns an admitted nonmatch after profile validation without dereferencing
the target or owner. No partial successful match survives a later bad ancestor.

The supported subset is canonical, non-generic fixed classes with no extended
flags. Interfaces as targets, arrays, strings, value types, generics, dynamic
types and finalizers are excluded. Interface/vtable/GC tails are not traversed;
class identity is neither hash/size equality nor general assignability. Both
classes need not derive from System.Exception. Callers retain exclusive stable
image, registration, object and receipt lifetime. The existing collecting ledger
rejects swept/replaced receipts; the predicate supplies no rooting or lifetime
extension and cannot authenticate fabricated compiler metadata or POD authority.

Seven focused host groups cover admission, identity, hostile inputs, bounded
hierarchies, unchanged backing/canaries and zero managed allocation. The actual
`RhTypeCast_IsInstanceOfException` export remains a halting placeholder. The
#575 leaf supplied no EFI Compile integration, guest proof, EH-record search,
exception construction or stack restoration. The separate #582 profile below
integrates the predicate for its bounded development catch; general EH and cctor
failure caching remain unsupported. #90 retains its unwind and real-execution acceptance gates.

## NativeAOT EH clause metadata

`NativeAotEhClauseMetadata.ReadClause` implements #579's host-only Windows
NativeAOT metadata leaf ([step1061](../done/step1061.md)). It revalidates the
original module and unwind snapshot, discovers the EH RVA in the pinned
ILC 10.0.12 trailer, and reads one physical clause only after validating the
complete table and every referenced funclet's nearest preceding root.
An intervening root rejects even an earlier selected row. Failure payloads
are zero; no-EH and out-of-range results have distinct statuses.

The format has low-bit compressed unsigned fields of one through five bytes,
fixed typed-target RVAs and optional associated-data before the EH pointer.
Windows handler flags determine whether the unwind tail is aligned and skips
a personality DWORD; an odd code count without those flags is not rounded.
Typed, filter and shared fault/finally kinds retain physical order, nested/equal
try ranges, end-exclusive containment and legal empty try-group boundaries.
CLROS limits a table to 64 rows and 4096 bytes. Nonminimal widths follow the
pinned permissive runtime decoder; this is not LEB128 or a Windows scope table.

The caller must supply exact same-image compiler code and EH contribution
extents, including funclets and folded aliases, with EH padding excluded.
Neither PE xdata nor the stream supplies the EH byte length. Numeric bounds,
root-row ends, section tails and the next symbol cannot establish that native
ownership. All image, registration and snapshot storage must remain mapped and
exclusively stable for each call. The reader takes no persistent EH snapshot.

Six original focused host groups pass, including the intervening-root
regression, late-error atomicity, unchanged inputs and zero managed allocation.
Typed-prefix range admission does not validate a class or perform matching.
Chains, reverse-PInvoke, unknown kinds/flags, split code and cross-module targets
remain unsupported. The leaf changes no EFI export/profile and supplies no
exception construction/rooting, handler search, invocation, stack restoration
or actual throw/catch/finally/rethrow. #579 is accepted and merged through
PR #581 as this bounded host metadata contract; the separate bounded #582 profile below adds finite execution proof without
extending the metadata reader's contract to general EH. #90 remains open.

## Implemented Behavior

- `OS.Std.CompatibilityStatus` records compatibility states.
- `OS.Std.Ascii` provides host-tested ASCII helpers for `char`, `string`,
  `byte`, and `ReadOnlySpan<byte>` inputs: `EqualsIgnoreCase`, `IsDigit`,
  `IsLetter`, `IsUpperLetter`, `IsLowerLetter`, `IsLetterOrDigit`,
  `IsPrintable`, `IsVisible`, `IsWhiteSpace`, `IsSpaceOrTab`,
  `ToUpperInvariant`, and `ToLowerInvariant`. The byte APIs are ASCII-only and
  leave non-ASCII bytes unchanged for casing.
- `OS.Std.Text.AsciiString` provides an OS-specific, allocation-free
  `ReadOnlySpan<byte>` helper slice for ASCII ordinal-ignore-case
  `Equals`, `EqualsIgnoreCase`, `StartsWithIgnoreCase`,
  `EndsWithIgnoreCase`, `IndexOfIgnoreCase`, `TrimSpaceOrTab`, and
  `TrimTrailingSpace`. Non-ASCII bytes are compared byte-for-byte and are not
  Unicode-folded. `TrimSpaceOrTab` trims only HTTP-style OWS bytes through
  `OS.Std.Ascii.IsSpaceOrTab`. `TrimTrailingSpace` trims only byte `0x20`; it
  intentionally preserves tabs and other ASCII whitespace for FAT short-name
  compatibility.
- `OS.Std.Text.AsciiTokenSplitter` provides a caller-owned ASCII token splitting
  helper for manifest/protocol-style byte spans. It validates source ranges and
  delimiters, preserves empty fields by default, optionally trims ASCII
  whitespace, optionally removes empty entries after trimming, reports required
  token capacity before writing, rejects non-ASCII/control input with the
  failing index, rejects unknown option bits, and returns status-coded results
  without allocating.
- `OS.Std.Text.AsciiKeyValueSplitter` provides a caller-owned ASCII key/value
  splitting helper over `ReadOnlySpan<byte>` inputs. It splits only on the
  first delimiter, returns source-backed `AsciiTokenRange` metadata for the key
  and value, preserves additional delimiter bytes inside the value, optionally
  trims ASCII whitespace with the existing `AsciiTokenSplitOptions` style, and
  rejects invalid ranges, invalid delimiters, invalid options, missing
  delimiters, empty keys, non-ASCII/control input, and default-result success
  without allocating.
- `OS.Std.Text.AsciiTextBuilder` provides a caller-owned ASCII byte formatting
  helper for diagnostics and protocol text. It appends checked ASCII bytes and
  chars plus decimal/hex integer values into caller-provided buffers, reports
  required byte counts, preserves overlap-safe byte copy through
  `OS.Std.Memory.BufferOperations`, and rejects non-ASCII/control input,
  undersized destinations, invalid offsets, and invalid hex digit widths with
  status-coded results.
- `OS.Std.Text.AsciiDecimalParser` provides allocation-free signed and unsigned
  decimal parsing for `ReadOnlySpan<byte>` ASCII input. It parses `int`, `long`,
  `uint`, and `ulong` values, rejects empty, sign-only, whitespace, non-digit,
  and overflow input with status-coded results, accepts a leading `-` only for
  signed methods, intentionally rejects `+`, reuses the shared `OS.Std.Ascii`
  digit classifier, and leaves default/zero results out of success evidence.
- `OS.Std.Text.AsciiHexParser` provides allocation-free unsigned hexadecimal
  parsing for `ReadOnlySpan<byte>` ASCII input. It parses `uint` and `ulong`
  values, accepts an optional `0x` or `0X` prefix, rejects empty, prefix-only,
  non-hex, and overflow input with status-coded results, and leaves
  default/zero results out of success evidence. Step1449 adds a separate
  `TryParseUInt64(byte*, uint, out ulong)` partial for freestanding callers:
  complete borrowed ASCII input, optional `0x`/`0X`, no allocations or exceptions,
  boolean success and zero output on failure. Null, empty, wrapping, malformed
  and overflowing ranges fail; the caller supplies readable storage disjoint
  from the output. No pointer `uint`, signed, culture or partial-token overload
  is provided. Host checks pass; original native and guest validation is pending.
- `OS.Std.Text.AsciiDecimalFormatter` provides allocation-free signed and
  unsigned decimal formatting for `int`, `long`, `uint`, and `ulong` into caller-owned `Span<byte>`
  buffers. It exposes digit-count helpers for the same signed and unsigned
  value shapes, reports required byte counts, rejects undersized destinations
  without writing partial output, and leaves default/zero results out of
  success evidence. Kernel/PAL diagnostic formatters should use this shared
  helper before adding any local integer-to-ASCII loops; the PAL export-catalog
  validation marker is host-tested against this shared path.
- `OS.Std.MemoryExtensions` provides a partial byte-oriented span helper slice: `Contains`, `Count(byte)`, `EndsWith`, `IndexOf(byte)`, `IndexOf(ReadOnlySpan<byte>)`, `IndexOfAny(byte, byte)`, `IndexOfAny(byte, byte, byte)`, `LastIndexOf(byte)`, `LastIndexOf(ReadOnlySpan<byte>)`, `SequenceEqual`, `StartsWith`, BCL-shaped `CopyTo(ReadOnlySpan<byte>, Span<byte>)`, and no-throw `TryCopyTo(ReadOnlySpan<byte>, Span<byte>)`. It also provides a narrow char-span helper slice for `IndexOf(char)`, `LastIndexOf(char)`, `SequenceEqual(ReadOnlySpan<char>)`, `StartsWith(ReadOnlySpan<char>)`, `EndsWith(ReadOnlySpan<char>)`, BCL-shaped `CopyTo(ReadOnlySpan<char>, Span<char>)`, and no-throw `TryCopyTo(ReadOnlySpan<char>, Span<char>)`.
- `OS.Std.Memory.BufferOperations` provides a host-tested partial byte-copy
  helper with status-coded `MemoryCopy(ReadOnlySpan<byte>, Span<byte>)`. It
  preserves memmove-style overlap behavior and rejects short destinations
  before writing.
- `OS.Std.Binary.EndianPrimitives` provides allocation-free span-based `UInt16`, `UInt32`, and `UInt64` little-endian and big-endian read/write helpers plus non-throwing `TryRead`/`TryWrite` variants for parser-facing kernel code.
- `OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa`
  provides an allocation-free, reference-free catalog of 25 explicit
  `UInt16` read vectors and seven explicit `UInt16` reverse-endian vectors.
  The five-byte and seven-`UInt16` source series are independently projected
  from the MOSA test-vector snapshot without compiling, copying, or importing
  its runtime. Caller-owned catalog copies are validated for exact counts,
  initialization, key range, duplicate keys, expected values, append-only
  ordering, and the frozen FNV-1a64 fingerprint `E81E4451E8D999F6` through
  status-coded results. Normal host builds preserve the span APIs. Under
  `NOSTDLIB_EFI`, the same catalog exposes only caller-owned unsafe pointer
  copy/validation APIs with explicit counts and capacities, including distinct
  fail-closed null-pointer statuses; that branch uses no spans, arrays,
  exceptions, allocation, reflection, or dynamic invocation. The catalog adds
  no canonical `System.*` API and cannot satisfy completion Gate 4.
- `OS.Std.Memory.SpanOverlapMemoryCastUnsafeWidthConformanceVectors` provides
  an allocation-free, reference-free catalog of exactly 16 deterministic
  vectors: four slice boundaries, five memmove-style overlap shapes, three
  distinct memory-cast shapes (divisible byte-to-`ushort`, combined unaligned
  byte-to-`uint` with trailing-byte truncation, and `uint`-to-byte expansion),
  and four unaligned access widths (`1/2/4/8`). Every cast binds both its exact
  output bytes and result-element count. The catalog records successful and
  rejected slice boundaries, both overlap directions, equal and zero-length
  ranges, trailing-byte and unaligned `MemoryMarshal.Cast` behavior, and unsigned
  `Unsafe.ReadUnaligned<T>`/`Unsafe.WriteUnaligned<T>` widths followed by zero
  extension, sign extension, widening, native-width preservation, and
  truncation. Its cast policy explicitly allows unaligned starts and uses
  floor division to discard only an incomplete trailing destination element.
  It validates exact count,
  initialization, key and operation ranges, malformed metadata, duplicate and
  append-only key order, and frozen FNV-1a64 fingerprint
  `F03FF59D36F39ECD`. Copy and `ExecuteCatalog` validate every pointer/span,
  count, capacity, alias range, catalog entry, and fingerprint before mutating
  caller-owned scratch or output storage. Execution emits 16 POD result records
  with operation status, exact output bytes/count, result-element count, raw
  unsigned width value, scratch observation, and rejected-slice preservation.
  Host builds expose only `Span<T>` APIs; `NOSTDLIB_EFI` exposes unsafe
  pointer/count/capacity APIs with distinct null, negative, undersized, address
  wrap, alias, catalog, and execution statuses. That branch uses no arrays,
  spans, exceptions, reflection, LINQ, or managed allocation. The POD catalog
  adds no canonical `System.*` surface, requires no reference files at runtime,
  fixes the audited pointer width at eight bytes, and does not satisfy
  completion Gate 4.
- `OS.Std.Hash.Crc32` provides an allocation-free reflected IEEE CRC32 helper using polynomial `0xEDB88320`, initial state `0xFFFFFFFF`, and final XOR `0xFFFFFFFF`. It supports one-shot `Compute`, incremental `Update`, and `Finish` over caller-provided `ReadOnlySpan<byte>` buffers without lazy static lookup tables.
- `OS.Std.Net.InternetChecksum` provides allocation-free 16-bit ones-complement internet checksum support for network-order packet buffers. It supports one-shot checksums, two-, three-, and four-segment checksum overloads, validity checks, and an `InternetChecksumAccumulator` that preserves odd pending bytes across segment boundaries without allocating.
- `OS.Std.NativeInt` provides a small pointer-sized unsigned arithmetic helper slice over `nuint`: checked add/subtract, power-of-two detection, alignment checks, and checked align-up/align-down results. It uses status-coded results so kernel/runtime planning paths can avoid exception-driven overflow handling.
- `OS.Std.Math` provides a host-tested partial integer helper slice for BCL-shaped `Min`, `Max`, `Clamp`, and `Abs` behavior over `int`, `uint`, `long`, and `ulong` where applicable. `Clamp` follows the BCL invalid-bound throwing behavior, and `Abs(int.MinValue)` / `Abs(long.MinValue)` throw overflow rather than wrapping.
- `OS.Std.ArrayHelpers` provides a host-tested partial unmanaged-span helper slice for `Clear`, `Fill`, `IndexOf`, `LastIndexOf`, BCL-shaped `BinarySearch` insertion-point results, full-span `Sort`, status-coded range-limited `TrySort`, and status-coded `TryBinarySearch` over caller-specified ranges. The current slice is no-throw, allocation-free, deterministic for small buffers, and constrained to unmanaged element types.
- `src/OS/Runtime/MinimalRuntime.cs` defines the current compiler-required primitive stubs, throw helpers, memory helpers, fail-fast type-cast placeholders, `IDynamicInterfaceCastable` metadata shape, and reverse-P/Invoke exports needed by the explicit EFI shell project.
- `OS.Std.NoRuntime.DotNetN10SystemExportCompatibilityLedgerImplementedStubbed`
  records a source-bound compatibility snapshot for all 50 current canonical
  `System.*` declarations and all 21 `[RuntimeExport]` methods in
  `MinimalRuntime.cs`. It uses readonly reference-free entries and
  switch-indexed retrieval without arrays, reflection, LINQ, exceptions, or a
  canonical `System.*` namespace surface. Unknown indices and default entries
  fail closed. The ledger classifies the runtime exports as four link-only
  no-ops, twelve fail-fast helpers, one audited stack-probe helper, and the
  profile-bound fixed-allocation, two SZ-array and string exports; the ledger itself adds
  no runtime behavior and cannot satisfy completion Gate 4.
- `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`,
  `BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, and
  `NativeAotHeapBootstrapPlanner` are host-tested prerequisites for future
  runtime allocation work. They convert successful boot page free-segment
  planning into caller-owned `BootPageFreeListNode` metadata, value-type
  allocator state metadata, caller-owned free-list mutation evidence, aggregate
  boot readiness marker metadata, and future managed-heap page-budget metadata
  without allocating live pages, and their readiness results reject default/zero
  values as successful evidence. They do not implement NativeAOT object
  allocation helpers, GC ownership, or live heap mutation.
- `NativeAotClassConstructorStateTable` provides a host-tested,
  allocation-free class-constructor state table over caller-owned storage. It
  uses numeric module/type keys, explicit value-type states, and status-coded
  transitions for register, begin, mark-initialized, mark-failed, and readback.
  It detects reentry cycles, failed cctor reuse, duplicate keys, missing keys,
  capacity exhaustion, invalid keys, missing failure codes, and default-result
  success bugs without executing constructors, delegates, reflection, dynamic
  invocation, allocation, or exception-based control flow.
- `NativeAotClassConstructorStateDiagnosticFormatter` writes initialized
  `NativeAotClassConstructorStateTableResult` metadata into caller-owned ASCII
  byte buffers with the stable `NATIVEAOT_CCTOR_STATE status=` prefix. It
  reports required bytes, rejects short destinations before writing, and rejects
  default/uninitialized results without allocating or throwing for normal
  formatter failures. Its byte-count accounting uses the shared
  `OS.Std.Text.AsciiDecimalFormatter` digit-count helpers, and its caller-owned
  write path uses `OS.Std.Text.AsciiTextBuilder` appends instead of
  formatter-local decimal or byte-copy loops.
- `NativeAotClassConstructorRunnerPreflight` provides a host-tested,
  allocation-free preflight contract for the future class-constructor runner. It
  validates caller-owned state-table initialization, type counts, target
  metadata counts, missing target metadata, pending/reentrant `Initializing`
  states, failed constructor states, and invalid states without executing
  constructors, discovering metadata, invoking delegates, using reflection, or
  relying on exception control flow. Its diagnostic formatter emits the stable
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status=` marker into caller-owned ASCII
  buffers and reports short destinations before writing. Its byte-count
  accounting uses the shared `OS.Std.Text.AsciiDecimalFormatter` digit-count
  helpers, and its caller-owned write path uses
  `OS.Std.Text.AsciiTextBuilder` appends instead of formatter-local decimal or
  byte-copy loops. `BootSequence` can emit host-tested
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` readiness markers, and QEMU tooling
  exposes an opt-in `cctor-preflight` marker preset for future runtime-focused
  smoke images. These remain host-tested preflight/formatter/marker contracts
  only; they do not execute class constructors, implement managed exception
  handling, or complete NativeAOT runtime support.
- `NativeAotClassConstructorWorkItemPlanner` turns an already-successful
  preflight result plus caller-supplied target metadata into caller-owned
  `NativeAotClassConstructorWorkItem` records. The records carry only
  module/type ids, nonzero `nuint` target tokens, and source state-table slot
  indexes for future dispatcher use. The planner rejects default preflight
  results, failed/pending preflight states, missing target metadata, zero target
  tokens, and insufficient destination capacity without invoking delegates,
  discovering metadata, executing constructors, or using exception control flow.
  `NativeAotClassConstructorWorkItemPlanDiagnosticFormatter` writes the stable
  `NATIVEAOT_CCTOR_WORK_ITEMS status=` marker into caller-owned ASCII byte
  buffers, reports required byte counts, rejects default/uninitialized plan
  results, and rejects undersized destinations before writing. Its byte-count
  accounting uses the shared `OS.Std.Text.AsciiDecimalFormatter` digit-count
  helpers, and its caller-owned write path uses
  `OS.Std.Text.AsciiTextBuilder` appends instead of formatter-local decimal or
  byte-copy loops.
  `BootSequence.RunNativeAotClassConstructorWorkItemReadiness` exposes the same
  already-produced plan metadata through host-tested
  `NATIVEAOT_CCTOR_WORK_ITEMS_READY` / `NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY`
  markers without wiring default boot, QEMU presets, constructor execution,
  delegates, reflection, exception control flow, or metadata discovery.

## EFI-Only Shim Boundary

The canonical no-stdlib `System.*` stubs are confined to `src/OS/Runtime/MinimalRuntime.cs` and are compiled only by the explicit EFI project `src/OS/OS.csproj`.

Boundary rules:

- `src/OS/OS.csproj` has `NoStdLib=true`, `DisableImplicitFrameworkReferences=true`, `EnableDefaultCompileItems=false`, and explicit `Compile` includes.
- Normal host-testable projects such as `src/OS.Kernel` and `std/no-runtime/shared` must not compile `Runtime/MinimalRuntime.cs`.
- Host-safe shared helpers live under `OS.Std.*` first, not under canonical `System.*`, until compatibility is complete and documented.
- The EFI shim can contain compiler-required placeholder exports, but placeholders must fail fast or provide real behavior. They must not silently return success for unsupported runtime features.

## Runtime Helper Ledger

`src/OS.Kernel/Runtime/NativeAotHelperLedger.cs` is the host-testable ledger for NativeAOT helper status. It records helper symbol or contract detail, evidence, required contract, allowed milestone stub behavior, current test coverage, owner subsystem, next step, and completion-gate applicability only; it does not implement helpers and does not make EFI-only stubs count toward kernel/runtime completion.

`std/no-runtime/shared/DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs`
is the allocation-free declaration/export classification companion to that
planning ledger. Its 51 append-only declaration identities bind current
`MinimalRuntime.cs` declarations, distinguish generic arities, and carry stable
unknown, narrowly-implemented, stubbed, or forbidden support states. Its 21
export identities preserve source order: four link-only transition no-ops,
eleven fail-fast type-cast helpers plus `RhpFallbackFailFast`, the audited
`RhpStackProbe` bridge, the profile-bound `RhpNewFast` export, then the two
appended SZ-array and string exports. The guarded empty Exception identity
is classified as Stubbed and adds no general exception services. The appended
empty UnmanagedType identity is also Stubbed; it supplies only the unmanaged
constraint modreq identity and no marshalling constants or services. Identity51,
empty InAttribute, is Stubbed and supplies only ref-readonly compiler metadata.
The fixed allocator
defaults to self-contained failure; its opt-in adapter is implemented and
artifact-tested in step946, with bounded real guest acceptance in step949.
Step952 adds the array host contract and focused artifact checks; step954
accepts the complete integrated host gate and bounded array guest evidence. These
entries do not copy the shim into normal host builds or satisfy general
allocation, GC, dispatch, exception or runtime-completion requirements.

| Helper area | Current status | Evidence / boundary |
| --- | --- | --- |
| Primitive/CoreLib type stubs | Implemented only for EFI compile boundary | `src/OS/OS.csproj` explicitly includes `Runtime/MinimalRuntime.cs`; host boundary tests verify it is not compiled by `src/OS.Kernel` or `std/no-runtime/shared` |
| `System.Runtime.RuntimeExportAttribute` | Implemented only for EFI runtime export metadata | Needed by `EfiMain` and NativeAOT helper exports |
| `System.Buffer.MemoryCopy` / `Memmove` | Implemented as byte loops for EFI shell needs | Used for no-stdlib memory copy paths; not a complete BCL buffer contract |
| `System.SpanHelpers` clear/fill/memmove slice | Implemented as narrow byte/pointer helper slice | Supports current EFI publish boundary only |
| Object allocation helpers | Bounded opt-in EFI allocation guest evidence accepted | Step940 proves the fixed caller-buffer leaf over the original #75 retained authority. Step946 adds the real profile-bound `RhpNewFast` adapter. Step949 accepts the complete host suite and five fresh QEMU profiles, including four compiled-new/six direct routes, before-bind rejection and exhaustion, with independent metadata/native/prefix/transcript/lifecycle/disk review. The general `ObjectAllocation` gate remains deferred; no general heap or GC is established |
| Array allocation helpers | General support deferred; bounded opt-in EFI guest evidence accepted | Step952 adds the shared retained-page SZ-array leaf and separately classified `EfiSzArrayAllocation` exports. Step954 accepts the complete host gate and eight fresh profiles with independent native/metadata/prefix, transcript/lifecycle and disk review. Genuine byte/long allocations, length/index/store readback, before-bind rejection and shared-page exhaustion are bounded guest observations. General `ArrayAllocation` completion, managed reference stores and GC remain deferred |
| String allocation helpers | Bounded retained-page EFI String guest accepted | `StringAllocation` remains incomplete. Step964 accepts the separately classified `EfiStringAllocation` path, compiled constructors and UTF-16 storage, with default/fixed/array regressions and three String rejection cases; general String/BCL, GC and write barriers remain unsupported |
| Write barriers | General runtime support deferred; bounded returning-helper guest progress | Step967's opaque-word host leaf remains distinct from step977's profile-bound authenticated reference route and managed bulk bridge. Step981 accepts two live sentinel words/five checks in the bounded noncollecting profile; ledger entry `WriteBarriers` remains incomplete and no collecting-GC barrier support is established |
| Interface dispatch | Bounded ordinary-instance dispatch accepted | Step1063 accepts the finite M04-016 native/guest slice and same-image rejection control. General ledger entry `InterfaceDispatch` remains incomplete; unsupported shapes and EFI type-cast helpers still fail fast |
| Class constructors | Bounded eager/lazy execution accepted | Steps1053/1063 accept actual M04-024 invocation over the retained TypeManager owner, profile-only compiler helpers and same-thread reentry. The general `ClassConstructors` ledger remains deferred: exceptions, cached failure/rethrow and cross-thread initialization are unsupported. Earlier host planning contracts alone do not execute constructors. |
| Thread statics | Deferred | Ledger entry `ThreadStatics`; no TLS bootstrap or NativeAOT thread-static base helper exists |
| Exception handling/unwind | General support deferred; separately qualified bounded diagnostics | Ledger entry `ExceptionUnwind` remains incomplete. Step1181 accepts two ordered nonthrowing compiler filters, a selected catch and compiled continuation; earlier steps cover bounded catch/finally/rethrow. General EH, filter throws, nested filters and collided unwind remain incomplete |
| Module initialization | Deferred | Ledger entry `ModuleInitialization`; no kernel/runtime module initialization helper behavior is implemented |
| TypeManager/RTR scans | Deferred | Ledger entry `TypeManagerRtrScans`; step989 accepts one bounded current-image module and slot-derived consumer in QEMU, while general registry/scanning and hosted runtime support remain unaccepted |
| GC stack roots | Deferred | Ledger entry `GcStackRoots`; stack-root registration/scanning helpers are not implemented |
| EFI reverse P/Invoke helpers | EFI-only link stubs | Ledger entry `EfiReversePInvoke`; `RhpReversePInvoke`, `RhpReversePInvokeReturn`, `RhpPInvoke`, and `RhpPInvokeReturn` exist only in the EFI shim and do not satisfy Gate 4 |
| EFI type-cast helpers | EFI-only fail-fast stubs | Ledger entry `EfiFailFastTypeCasts`; `RhpFallbackFailFast` and `RhTypeCast_*` helpers halt and do not satisfy kernel/runtime completion |
| EFI stack probe | EFI-only audited bridge/byte plan | Ledger entry `EfiStackProbe`; M01-021 source/readback tests and emitted-object audits cover the fail-closed `RhpStackProbe` bridge, exact 27-byte page-probe plan, and generated pre-transfer frame budget. This is source/build evidence only: no guest execution or kernel/runtime completion is claimed |

## Unsupported Behavior

The full BCL/runtime surface remains incomplete beyond the accepted bounded
helper, retained-heap/collector, constructor, module and dispatch slices described
above. General exception handling, thread statics, hosted .NET 10, ASP.NET
Minimal API hosting and Kestrel remain unsupported. The `OS.Std.NoRuntime`
declaration/export ledger is classification metadata only: it does not make
the EFI shim callable by the kernel, replace the canonical `System.*` stubs,
or promote a classified entry into full Gate 4 acceptance.

### M04-004 integer operator conformance

`OS.Std.Numerics.IndependentArithmeticShiftCastN64BitDivisionRemainder`
provides 57 fixed vectors across 30 operations: unsigned 32/64-bit wrapping
arithmetic, signed/unsigned right shifts and left shifts, signed/unsigned
8/16/32-bit casts, numeric enum round trips for all eight integer underlying
types, and signed/unsigned 64-bit division/remainder. It is a conformance
runner, not a replacement for C# operators or a canonical `System.*` API.

`TryGetVector` returns one immutable POD by index and clears its output for an
invalid index. `Evaluate` takes runtime operands through a non-inlined method;
`RunCatalog` executes every vector and checks the ordered input/result FNV-1a64
fingerprint `45FDFA24DD72EA28`. All result defaults fail closed. No method
allocates, uses references, reads reference files, or throws intentionally.
Unknown operation codes, zero divisors, and the signed minimum/-1 pair return
explicit statuses and zero output. The last pair is deliberately rejected for
both division and remainder before executing either potentially trapping
operation. This is the runner's no-exception policy, not proof of CLR exception
behavior or the general remainder contract.

Inputs and outputs are raw `ulong` patterns. Signed results extend their sign;
unsigned 32-bit arithmetic truncates operands and zero extends results.
Arithmetic/casts are explicitly unchecked; shift counts obey C#'s 31/63 masks,
including negative and oversized counts. Enum conversions allow unnamed values;
they do not implement enum metadata, validation, boxing, or `Enum.IsDefined`.
Checked overflow, floating-point, native-width integers, managed exception
delivery, allocation, and general runtime/BCL completeness remain out of scope.

The host module independently checks literals and 6,300 operand/operation
combinations with `BigInteger`, all unsupported operation codes, invalid
indices, default values, and zero-allocation repeated execution. A separate
C# 14/.NET 10 Release compilation with no standard library verifies that the
same production file has zero assembly references. That is compiler/metadata
evidence, not a NativeAOT EFI publish or guest execution.

Historical `references/SharpOS-master/Kernel/Tests/CS/` supplied categories
only. No source or test body was copied, and no reference is needed to run the
tests. These source headers declare GPL v3 with a Classpath exception; this is
not the modern CC0 SharpOS repository. Snapshot revision is unknown. Reviewed
SHA-256 values:

| File | SHA-256 |
| --- | --- |
| `Arithmetic.cs` | `59F153E85A97ECD4773EAE7BF399F32825848DE7CF45D2A94AB1B2069A13CA80` |
| `BitwiseOperators.cs` | `EF960FF7128461C8BF87BC92303296C6689408BFA9E5232F3B776741F4B701D8` |
| `SignedIntegerCast.cs` | `EB1957E8CB602FAAB4156595402218A0D431D49BDDC1E2EB7D8BF5020F30566C` |
| `UnsignedIntegerCast.cs` | `D1DA1013BC99C5455B0E84D57885A910C13D74889272F9CD3EA0F6C39EE94984` |
| `Enum.cs` | `5012D6E09EECB3D8ED7FCA2EB7C7EFF5E6184F89CE847C2A69ED4F08301D7E57` |

The historical 64-bit division helper returns its dividend and explicitly
records that division is unimplemented; it is not an oracle. CLROS uses
independent literals, arbitrary-precision host calculations, and the documented
[C# arithmetic](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators),
[shift](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators),
and [conversion](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions)
contracts.

The host envelope is `TEST_START m04.004`,
`CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail`,
then `TEST_PASS m04.004`; failure emits `TEST_FAIL m04.004 <reason>`.
Supplemental #507 now links this unchanged source into a default-off EFI
profile with explicit M04-002/M04-003 prerequisites and a 68-item/81-input
source closure. A concrete CS0117 failure required only the standard
`NoInlining = 0x0008` compiler-attribute enum member; actual evaluator metadata
retains that flag without framework assembly references. The adapter calls
the catalog once and checks status, count 57, failed index -1, and the frozen
fingerprint before its distinct `hostConformance=false guestIntegrated=true`
record. Step928 records publishes and controlled adapter checks. Step929 adds
the complete 3,517-pass host gate and independently reviewed fresh QEMU/OVMF
proof from source `07da84eb0305b680d43f72f4fd690d5ed6fe3e18`: default has no M04
records; enabled executes the exact catalog once with the expected fingerprint
and order. Its serial SHA-256 is
`0BC55D0331B587FEBC333283C4EC0F7F8F211B4747675C705511CF59D713AFFC`.
Gate 4 remains **Fail**; no exception-delivery, GC, allocation, or general
runtime capability follows from this catalog. #73/#507 closure follows the
reviewed integration merge.

### M04-005 version-pinned layout values

`ObjectArrayStringEETypeMethodTableLayoutContracts` evaluates scalar
descriptions of fixed canonical objects, zero-based single-dimensional arrays
and strings for x64 little-endian NativeAOT 11.0.0-rc.2.26504.105. It uses
`dotnet/dotnet` source commit `820230c386db5b0106c5430b3e5aee92a7826bf5`,
identified by both pinned compiler/runtime package metadata. The source
authorities are `ObjectLayout.h`, `MethodTable.cs`, `MethodTable.Constants.cs`,
`EETypeBuilderHelpers.cs`, `TargetDetails.cs`, `GCHelpers.cpp` and
`String.NativeAot.cs`. Reviewed hashes of the downloaded primary sources are
recorded as ignored evidence; they are not runtime dependencies or committed
first-party source.

Offsets explicitly name their origin. Total raw/aligned sizes start at the
allocation, including the eight-byte header before the object reference.

| Item | Origin | Offset or size |
| --- | --- | --- |
| Header | Object reference | -8; eight bytes |
| Method-table token | Object reference | 0 |
| Fixed-object data | Object reference | 8; minimum total size 24 |
| Array/string length | Object reference | 8 |
| SZ-array data | Object reference | 16; total base 24 |
| String UTF-16 data | Object reference | 12; raw base 22 includes one terminator |
| Fixed method-table prefix | Method-table start | 24 bytes |
| Flags/base/related token | Method-table start | 0 / 4 / 8 |
| Vtable count/interface count/type hash | Method-table start | 16 / 18 / 20 |

`Evaluate` admits one explicit compiler/target profile, checks shape and
supported flags, validates base/component/count limits, computes raw size and
eight-byte alignment, and compares a caller-declared numeric capacity. It
reads no address and proves no actual storage availability. The prefix is a
POD observation, not a native memory overlay. Related-type tokens and optional
metadata counts are opaque; their values do not establish usable pointers,
tail storage or dispatch support.

The low ushort is a component size only when `HasComponentSize` is set.
Supported fixed objects allow low extended flags zero or `0x0008`; this
prevents the SharpOS-reference regression where nonzero low flags could be
mistaken for a component size. The selected .NET 10 encoding identifies kind
1 as a function pointer; it is unsupported here. Reserved/inconsistent
encodings fail as invalid, while recognized unsupported shapes or dynamic,
generic, variance and finalizer features receive unsupported statuses.

Maximum counts are `0x7FFFFFC7` for SZ arrays and `0x3FFFFFDF` for strings.
Multiplication promotes before evaluation; the largest admitted count times
a ushort component fits ulong, including base and alignment. Numeric capacity
is not a heap reservation. Failures return zero result payload, defaults do
not succeed, and no allocation or writable input is involved.

The host tests use 13 independent literal size rows, byte-layout fixtures,
all 65,536 low-ushort encodings under both fixed/array interpretations,
invalid profile/flag/shape/count/base/capacity cases, default/reference-free
results and 40,000 warmed evaluations with zero allocations. The same actual
source compiles with zero assembly references under the no-stdlib harness;
metadata preserves `NoInlining` on `Evaluate`. This is host/compiler evidence,
not inspection of a compiler-emitted managed object or guest execution.

```text
CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail
```

Supplemental #512 implements the default-off EFI adapter with 13 positive
fixtures and six rejection cases, comparing every field of all 19 actual
NoInlining evaluations. Controlled mismatch and transcript checks pass; the
69-Compile/82-input closure preserves the frozen leaf and existing runtime
shim. The source firewall admits only its exact reviewed path/content identity.
Step933 adds the complete host gate (3,527 starts, 3,528 passes, zero failures)
and independently accepted fresh default/layout NativeAOT QEMU evidence at
source `5f6108ca363f03c7ebad8dafca7ae78e1d3a0db2`. The enabled guest runs all
19 scalar checks and emits the exact record once after M04-004 and before map
capture; default emits none. Layout serial SHA-256 is
`832AE32F7EDBC61E938D1FF498ED5CE23F120E8D474B7B3FD78A8917D825C57D`.
Issue closure follows the reviewed merge. The known #514 FAT geometry defect
is disclosed in `docs/qemu.md`; embedded EFI identity is independently checked.
Allocation, GC, type discovery, optional-tail traversal, dispatch,
exception/unwind support and hosted .NET/Kestrel remain unsupported.

See step931 for source identities, commands, setup failures and the pending
combined full-gate boundary.

### M04-006 retained heap-page contract and EFI adapter

`ManagedHeapPageRangeLiveAllocator` reserves one 4096-byte page through the
existing `AllocateOnePage` mutator and publishes its AfterState into the
original caller-owned allocator slot. It retains the original node, ledger,
parent and backing identities, zeroes the actual mapped page, and independently
reads every byte through a private `NoInlining` fixed-page helper before
marking the 216-byte POD state ready. The result is a 160-byte POD snapshot.
No release/reset operation or allocator restore receipt is exposed.

`ValidateRetained` reads the current bounded free chain and accounting. It
accepts legal later allocation, exhaustion and strict-LIFO release of only a
later allocation while rejecting any free extent overlapping the retained
page. Current zero contents are a separate observation from retained ownership;
legitimate writes do not make the page unowned. Reinitialization cannot clear
or release an already reserved page. Post-reservation failure keeps explicit
`ReservedNotReady` ownership.

Step935 records reviewed host tests using aligned real backing and separate
synthetic physical identities, all 4096 nonzero-byte positions, complete outside
canaries, unchanged ledger bytes, hostile spans/topology, and zero managed
allocation in warmed operations. The actual eight-source no-stdlib closure
has zero assembly references and preserves `NoInlining` on the reader. No new
shim or general memory utility was added.

Those tests provide host caller-buffer evidence. Stable exclusive original storage and
correct backing mappings remain caller obligations. Pointer identities and
free-list exclusion are not an unforgeable allocation registry, clone/ABA
protection or SMP synchronization. No managed object allocation, allocation
helper, GC, hardware NX, permanent paging or post-firmware lifetime follows.
Step937 integrates supplemental #516's disabled-default EFI adapter. It uses
the original M01 allocator, node and five-entry ledger after every historical
stage-two completion consumer. A stable heap state retains one page from the
original two-page LoaderData parent. Immediately before the boot plan, the
adapter reacquires the same slots and validates ownership and all 4096 current
bytes again. It checks original identities, ranges and accounting at both
checkpoints. Reserving the page changes M01's former completion accounting;
those historical completion records are neither weakened nor reissued.

Failure preserves the parent and any retained page and reaches the existing
terminal spin. There is no cleanup/free/reset, replacement firmware allocation
or extra global reservation. The ledger is fully validated without writes;
controlled tests compare all 200 bytes, but the adapter does not retain a
historical ledger snapshot. Step939 adds the accepted complete host gate and
fresh default/layout/heap QEMU matrix at source `4b18b4f`. The enabled guest
reserves one page from the original two-page parent, observes all 4096 bytes
zero after initialization and again before the boot plan, and retains the
same allocator, node, ledger, parent and heap-state identities. The exact
52-record transcript ends in the terminal boot markers without returning to
firmware. The default and standalone layout lanes preserve their earlier
returning behavior. Issue completion follows the reviewed PR #515 merge.
This proves the bounded pre-firmware page handoff, not object allocation,
runtime helpers, GC or post-`ExitBootServices` lifetime. Gate 4 remains **Fail**.

### M04-007 fixed allocation in caller-owned buffers

`GuestTestRhpNewFast` adds the host-testable allocation leaf for a retained
4096-byte page. Its 104-byte access value carries the original thirteen #75
arguments. Each accepted bind/allocation freshly calls `ValidateRetained`;
the original 216-byte heap ownership slot, allocator, nodes and ledger stay
unchanged. A separate 80-byte state binds its own address/capacity, original
heap slot, mapped/physical page and allocation-origin cursor/count. Binding
requires a zero state prefix and currently pristine page; later allocations
accept existing nonzero objects without clearing the whole page.

The pinned 10.0.12 win-x64 scalar policy admits the seventeen fixed element
values for ordinary/reference-bearing/generic classes, primitives, enums as
their underlying encoding, structs and nullable shapes. `HasPointers` and
`IsGeneric` are permitted. Value-padding and nullable-offset flags are checked
by category. Component types, noncanonical kinds, finalizers, dynamic metadata,
byref-like types, special alignment and unknown flags reject. This local policy
does not weaken the narrower frozen M04-005 evaluator.

For origin `B=P+U` and already aligned `BaseSize=S>=24`, allocation zeroes only
`[B,B+S)`, writes the supplied full-width MethodTable token at `B+8`, returns
that numeric reference value, and advances the offset by `S`. It adds no
second header. All ordinary checks precede writes; rejected results have zero
address/size/counter payloads. Full supplied capacities, aliases, overflow and
state consistency are checked; earlier objects, unused backing and canaries
remain unchanged. The token and related metadata are never dereferenced.

Step940 records focused tests with actual #75 authority, independent complete
byte oracles, all admitted elements and invalid alternatives, exact exhaustion,
high addresses/tokens, copied/corrupt state and zero CLR allocation. The actual
ten-source no-stdlib closure has zero assembly references. The host record is
`CLROS_M04_007_HOST_READY ... hostConformance=true guestIntegrated=false
compiledNew=false runtimeExport=false gc=false gate4=Fail`.

This is caller-buffer evidence, without a runtime export or real CLR object.
The caller owns readable/writable, stable, exclusive storage, actual mappings
and a single controller. Numeric/self-identity checks do not authenticate
MethodTables or prevent forged PODs, ABA or concurrent access. There is no
reset/free/grow, refill, OOM exception, boxing payload copy, write barrier or GC.
The leaf alone does not close #76. Its separately owned EFI implementation in
#519 now has the bounded physical guest acceptance recorded in step949 and the
following section; issue closure follows the reviewed merge. Gate 4 remains
**Fail**.

### M02-015 terminal double-fault integration boundary

The #521 adapter in [step950](../done/step950.md) links the unchanged double-fault
leaf and narrow false authority into the explicit EFI project. Its 79 Compile
items and 92 source inputs preserve the earlier allocation shim and runtime
exports. Default/enabled artifacts require zero AssemblyRefs and actual profile
isolation; the default artifact contains no compiled DF adapter. Enabled code
must retain fresh page-image audit, final readback, instruction publication and
indirect entry in that order. Neither emitted bytes nor compiled call order
alone proves CPU delivery. [step951](../done/step951.md) separately accepts the
fresh same-image native/current-state association and actual terminal DF
capture on original IST1, with default and standalone PF regressions. No general
exception, recovery, GC, runtime or post-ExitBootServices support is added;
Gates 3 and 4 remain Fail.

### M04-007 opt-in EFI allocation source boundary

Step946 links the unchanged host leaf to `RhpNewFastAllocation.cs` and the sole
NoInlining `RhpNewFast` runtime export. `EnableM04RhpNewFastProbe` defaults false;
the default export halts without returning a reference. Enabled operation uses
the original thirteen retained-authority arguments and original loaded-image
capture after the second whole-page-zero checkpoint. Each allocation performs
the leaf's fresh retained-ownership check. Access104, bump80, control96 and
records5872 are distinct POD slots; no second heap allocator is introduced.

The finite adapter matches the incoming token against ten actual same-build
`Of<T>` identities before reading a 24-byte MethodTable prefix. Its bounded
PE reader permits at most eight sections and requires the whole prefix in one
initialized, readable, nonwritable, nonexecutable, nondiscardable section.
Image mapping remains caller-attested; PE flags do not prove active page
permissions. Metadata tails and related-type pointers are not dereferenced.

Four compiled-new routes cover plain, reference-field, generic-reference and
generic-value classes. Six direct actual-export routes cover int, enum, fixed
value, reference-containing value, generic value and `Nullable<int>` shapes.
The latter are allocation-only: no boxing payload copy or nullable substitution
is implemented. Reference fields remain zero; constructors write scalars only.
The first ten allocations record actual header, MethodTable, zero-before-write,
cursor and object bytes, followed by typed/raw field and prior-object checks.
`referenceSlotBytes` counts all untouched payload/padding bytes excluding an
assigned scalar; it does not classify every byte as a managed reference.

Before-bind and exhaustion profiles require actual leaf statuses5 and15.
Rejected calls compare all4096 heap bytes and all80 bump-state bytes, then halt
without returning into a constructor. `M04_RHP_PREP` reports actual image and
scratch extents before either branch; `M04_RHP_BIND` denotes successful binding
only. Exact positive/before-bind/exhaustion grammars contain78/49/71 records.

Step949 accepts the complete host suite and five fresh QEMU profiles on source
`77860a689b8460114cf179e44dd442533a1ad638`. Positive allocation records ten objects,
used272/count10, four compiled-new and six direct routes, with scalar readback
and zero prior-object/canary mismatch. Before-bind returns leaf status5 with
zero cursor/count/constructors; exhaustion reaches used4088/count169 and rejects
with status15 without changing heap or bump bytes. Both rejection paths halt.
Fresh inspections of all five archived OS.dll files prove zero AssemblyRefs and
the exact export signature/attribute/body. Independent same-image ABI review
binds actual compiler calls and constructors; twenty full24-byte prefixes match
COFF/PE fixups under signed relocation. Before-bind proves one attempted token,
with no prefix observation. Whole raw transcripts, measured process termination
and pristine/booted disk accounting are separately accepted. This establishes
only the fixed finite fixture domain and retained one-page heap contract.
General allocation, refill/reclamation, GC/roots/barriers, arrays/strings,
cctors/dispatch/exceptions, post-firmware lifetime and hosted .NET remain
unsupported. The runtime ledger adds a narrow fifteenth entry without passing
the general allocation gate or Gate4.

### M04-008 shared SZ-array allocation host contract

`GuestTestArrayAllocationLengthOverflowChecks` is a thin entry into the same
private allocation path as `GuestTestRhpNewFast`. It retains the original
104-byte access value, 80-byte state, thirteen ownership arguments, one-shot
binding and fresh retained-page validation. Fixed and array calls consume the
same 4 KiB page; neither path owns another cursor or heap.

The pinned evaluator admits SZ-array base24 and component widths1/2/4/8, with
signed lengths from zero through `0x7FFFFFC7`. A pointer-containing shape is
limited to width8 and zeroed slots; reference stores and barriers remain
unsupported. Existing status values0..15 are preserved, with unsupported
array type, invalid component size and invalid length appended as16..18.
Metadata is supplied ordinary data at this boundary, not authenticated by a
numeric token. All rejection checks precede writes and preserve full declared
storage spans and state.

For allocation origin B, the reference is B+8, the MethodTable occupies
B+8..15, the four-byte length occupies B+16..19, padding occupies B+20..23 and
elements begin at B+24. The whole aligned allocation is zeroed before metadata
and length are written; cursor/count commit last. Host tests independently
check every byte, fixed/array interleaving, old objects, authority changes,
aliases, signed/count/size boundaries and exact one-page exhaustion. Byte[4072]
and width8[509] each exactly consume4096 bytes. Warmed calls allocate no CLR
objects. These are numeric references into caller-owned buffers, not objects
allocated by the host CLR.

The complete focused fixed/array host modules passed20 starts/21 passes with
zero failures. Their actual ten/eleven-source no-stdlib closures have zero
AssemblyRefs. The isolated pinned compiler experiment separately identifies
genuine byte/long `newarr` helper calls, the bounded Array length field/getter,
and length/data offsets from a reference. Its terminal link helpers prove no
successful returned allocation. The separate #523 integration adds the
default-off source profile below. Step954 records the accepted combined host
run and fresh array QEMU evidence; the earlier compiler experiment remains
compile/link evidence only.
General arrays, casts, exceptions, GC, refill and post-firmware lifetime remain
unsupported; Gate4 remains **Fail**.

### M04-008 opt-in EFI array source boundary

Step952 extends the existing allocation adapter with authentic byte/long
SZ-array and element tokens from the same build. Exact token checks precede
bounded full-prefix reads. The array and related element each require a complete
24-byte prefix in initialized, readable, nonwritable, nonexecutable,
nondiscardable image storage. Numeric observations and section flags do not
independently establish runtime page permissions or metadata authenticity;
same-image COFF/PE association and relocation remain separate evidence.

The minimal Array shim has the demonstrated first signed length field and
bounded getter. `RhpNewArrayFast` and `RhpNewPtrArrayFast` have the exact
`object(MethodTable*, nint)` ABI for the current .NET 11 compiler and default to
terminal failure. Step952's historical .NET 10 ABI used `int`; the migration
preserves the full signed native count until checked narrowing. The pinned
compiler routes the genuine byte and long allocations to the two helpers;
their names do not classify the presence of managed references. Both use the
same private shared allocator path. The helper ledger adds one narrow
`EfiSzArrayAllocation` entry and preserves the original eighteen exports as
the prefix of the twenty-entry source list. General allocation gates stay open.

The positive fixture allocates fixed24, byte[0]/24, long[3]/48, fixed32 and
byte[1]/32, ending at used160/count5. Typed scalar/length readback checks prior
objects and canaries. Ten expanded 224-byte observation slots, canaries and
the rejection snapshot occupy 6,352 bytes; this also replaces the older fixed
profile's 5,872-byte scratch capacity without changing its allocation sequence.
There is still one retained 4 KiB allocation page. Exhaustion adds 82 complete
fixed/empty-byte pairs and rejects a long[1] allocation at used4096/count169;
the before-bind branch rejects byte[0] at zero used/count. Rejection compares
the entire 80-byte bump state and 4,096-byte heap before halting.

The complete six-group array EFI initializer passed, including four actual
default/positive/before-bind/exhaustion NativeAOT publications, zero-AssemblyRef
checks and offline native route checks. [Step954](../done/step954.md) separately
accepts the 3,611-start/3,612-pass complete host run and eight fresh profiles on
source `1ce1889bab1bc9307b3904becdc66cb3d12078d0`. Genuine byte/long helper
returns, length/index/store readback, retained objects and both rejection paths
have independent same-image metadata/native, full raw/lifecycle and disk
association. Fixed ten-route and array six-role relocated prefixes match the
fresh COFF/PE inputs; before-bind has only two attempted tokens and no observed
prefix. Successful publications alone still do not prove execution or general
array behavior. See `docs/efi-build.md` and `docs/qemu.md` for prerequisites and
transcript boundaries.

### M04-009 shared String storage host contract

`GuestTestStringAllocationCharacterStorage.TryAllocate` takes a signed64 length
and enters the same private allocation path as fixed objects and SZ-arrays.
The 104-byte access, 80-byte state, 88-byte result, thirteen original authority
arguments, one-shot binder and fresh retained-page validation are unchanged.
There is one cursor and one allocation commit. Existing status values0..18 are
preserved; `UnsupportedStringType` is appended as19.

The pinned evaluator must admit actual String shape, base22, component2 and no
GC pointers. The full signed count is checked against zero and `0x3FFFFFDF`
before narrowing. Invalid counts retain metadata-before-length rejection
precedence. The selected size is `align8(22+2*n)`: at allocation origin B, the
reference/MethodTable is B+8, length is B+16, characters begin at B+20 and the
zero UTF-16 terminator starts at B+20+2*n. The whole selected span is zeroed
before metadata and length writes; cursor/count commit last. Zero length
consumes24 bytes. Metadata supplied as scalar data is not authentic readable
runtime metadata, and numeric references remain caller-owned buffer addresses.

[Step955](../done/step955.md) records complete fixed/array/String focused modules
with31 starts/32 passes, zero failures and actual10/11/11-source no-stdlib
closures with zero AssemblyRefs. Literal full-span byte oracles cover embedded
NUL, non-ASCII and surrogate code units, preserved terminators/padding, mixed
cursor224/count7, exact2037-character page fill and2038-character rejection,
signed64 truncation, full declared-span aliases, original authority changes and
complete rejection storage preservation. Warmed real mixed calls allocate zero
CLR bytes. Normal HostTests Release compilation also passes; a combined full
suite is not part of this checkpoint.

The earlier pinned compiler experiment established `RhNewString(MethodTable*,
nint)`, constructor remapping and String layout/field offsets using a terminal
helper. It established no successful returned allocation. This host slice adds
no String runtime export, constructor, interning, character-copy operation,
refill or GC. Shared EFI source admission and same-image fixed/array
compatibility are accepted in [step959](../done/step959.md): the settled host
suite passes3,636 starts/3,637 passes with zero failures, and fresh fixed/array
images bind the current shared core, returns, constructors and22 observed
full metadata prefixes. This is existing fixed/array compatibility, not
String guest evidence. Step963 then added source and artifact evidence; the
subsequent step964 acceptance below supplies the bounded String guest proof.
Gate4 remains **Fail**.

### M04-009 opt-in EFI String integration

[Step963](../done/step963.md) adds `RhNewString(MethodTable*, nint)` to the
existing allocation adapter. The export defaults to terminal failure and is
enabled only by the explicit String profile. Genuine compiler-created strings
use the bounded `String.Ctor(char, int)` path, length/index readers and the
original retained-page allocator. The String and related Object tokens require
full 24-byte prefixes from the same image. The entry handoff, original binder,
single cursor/commit and four accepted host dependencies remain unchanged.

The positive fixture creates one fixed object, one byte array and five strings
of lengths 0/1/3/3/2, ending at used200/count7/constructorCount6. Character cases
include ASCII, Omega, embedded NUL and unpaired surrogate code units. Length,
UTF-16 storage, zero terminator/padding and earlier objects are checked. Shared
observation slots are now256 bytes and scratch storage6672 bytes; access104,
bump80 and control96 remain unchanged. The same scratch change requires fresh
fixed/array regressions in the combined gate.

Before-bind rejects without allocation. Exhaustion adds161 empty strings and
one length-three string, fills4096 bytes at count169/constructorCount168, then
rejects length1. The direct invalid-length case passes full-width4294967297 to
the helper and rejects before a constructor. Rejections compare the complete
state and retained heap. The helper ledger appends EFI-only area17/status8;
six EFI-only entries remain excluded from general runtime completion. The
canonical shim ledger at that string milestone reported48 declarations and21 exports;
#582 subsequently appends the guarded Exception identity as declaration49.

At step963, the complete managed harness passed 63 starts/64 passes; the
separate complete String EFI harness passed 6 starts/7 passes, including five actual NativeAOT
publications with zero OS AssemblyRefs. Metadata and linked-code checks cover
the constructor/helper/readback paths, full String/Object prefixes and
relocations, and the signed64 invalid-length route. These are host and compiler
results.

[Step964](../done/step964.md) accepts the complete 3,653/3,654/0 host gate and
seven fresh captures at `be2762f`. Independent current-image review maps 415
selected methods and all 28 allocation exports. Returning routes preserve RAX
through the real shared adapter; genuine String construction supplies the
authentic String token and writes UTF-16 through the returned reference.
The invalid-length route preserves all 64 bits of 4,294,967,297 and rejects
before any constructor; before-bind never binds. Both retain zero allocation
and constructor counts. Exhaustion observes the complete 4,096-byte page fill,
then unchanged 80-byte state and 4,096-byte page on rejection.

Fresh joins cover 26 observed full 24-byte prefixes: fixed 10, array 6 and String
positive/exhaustion 5 each. Before-bind/invalid-length bind only their two
attempted tokens, with no observed prefix words. All seven linked images/maps
match accepted static evidence; only fixed/array also have whole managed-DLL
and COFF identity. The other five use fresh managed metadata and nondebug
COFF/symbol/relocation/unwind comparisons; changed debug records are not
silently treated as whole-file equality. No interning, general String/BCL
surface, barriers, collecting GC, refill, real ExitBootServices, hosted .NET or
Kestrel is claimed. Gates3/4 remain **Fail**.

### M04-010 retained-page store and bulk-move host prerequisite

`CheckedReferenceBulkBarriersIncludingDotNetN10Bulk` provides `TryStore` and
`TryMove` under the explicit `NonCollectingExclusiveRetainedPage` profile.
Both operations revalidate the original thirteen `RhpNewFastHeapAccess` inputs
through `ManagedHeapPageRangeLiveAllocator.ValidateRetained`. They use the
actual retained 4 KiB page without another allocator, binder or cursor.

Stores write one aligned eight-byte word. Moves accept full-width unsigned
byte offsets/counts, validate both complete spans before pointer arithmetic,
and use eight-byte loads/stores with memmove direction for overlap. Valid
zero-length and exact-self moves write no bytes but still validate authority;
that existing validator reads the whole page. Failure precedence is authority,
profile, offset alignment, count alignment, then range. Default results fail.

Payloads are opaque numeric bits, including zero, high-bit words and scalar
data. Page membership does not authenticate an object or reference field.
Mapping, lifetime and exclusive access remain caller obligations. The leaf
rejects destinations outside that page; it is not a drop-in checked-reference
runtime helper. It provides no collection, remembered sets, publication fences,
GC polling, concurrent access or special byref-helper ABI.

[Step967](../done/step967.md) records the complete five-module focused host
run: 43 starts, 44 passes including its terminal marker, and zero failures.
Rejection fixtures compare all 64 KiB after each call; other checks verify both
overlap directions and mixed fixed/array/String allocations, preserve allocator
and cursor bytes, and measure zero warmed CLR allocation. The normal host-test
build and actual eleven-source C#14/.NET10 no-stdlib compile pass, with zero
AssemblyRefs and four inspected leaf IL bodies. The clean common suite on
`854a335` also passes 3,670 starts/3,671 passes/zero failures
([step970](../done/step970.md)). These remain host/compiler results. The later
returning-helper guest progress is recorded in [step977](../done/step977.md),
and [step981](../done/step981.md) accepts the later live two-word/five-check
stack evidence for the bounded M04-010 profile. The general `WriteBarriers`
ledger remains incomplete; Gate4 stays **Fail**.

The separate #536 host-tool prerequisite adds an explicit opt-in to
`Amd64CoffWriter` for bounded REL32 fixups to undefined external functions.
Its original overloads still reject that form, and the existing instruction
serializer remains exactly 336 bytes with its original hash. The complete new
writer and unchanged serializer initializers passed 2 starts/3 passes/zero
failures on their retained isolated inputs; the imported exact source also
builds in the canonical generator and HostTests projects. [Step971](../done/step971.md)
records this integration and its earlier test failure. The combined common gate
now passes at `eeee523`; [step977](../done/step977.md) accepts this bounded
host-tool prerequisite; PR538 later merged and #536 closed with evidence.
Encoding the object alone does not prove a returning barrier or runtime ABI.
[Step981](../done/step981.md) separately accepts the bounded #79/#537
returning route and its live executing-stack sentinel evidence.

## Tests

`tests/OS.HostTests/StdAsciiTests.cs` covers ASCII character classification,
casing, upper/lower letter helpers, space-or-tab classification, and non-ASCII
pass-through behavior for the current helper slice. It emits
`TEST_PASS std.ascii upper lower letter helpers` and
`TEST_PASS std.ascii space-or-tab helper` for the focused helper additions.
`tests/OS.HostTests/StdAsciiByteTests.cs` covers byte classification, including
printable `0x20..0x7E` and visible `0x21..0x7E` boundaries, casing, and
case-insensitive span equality without converting protocol bytes to strings.
`tests/OS.HostTests/StdAsciiStringTests.cs` covers ASCII ordinal-ignore-case
prefix, suffix, and search behavior over `ReadOnlySpan<byte>` inputs, including
empty needles, too-long needles, repeated matches, and non-ASCII byte
pass-through. It also covers equality helpers, `TrimSpaceOrTab` for HTTP OWS
semantics, source-guarded use of `OS.Std.Ascii.IsSpaceOrTab`, and
`TrimTrailingSpace` for empty, all-space, no-trim, partial-trim, and
tab-preservation cases. It emits `TEST_PASS std.ascii.string startswith
endswith ignorecase`, `TEST_PASS std.ascii.string equality helpers`,
`TEST_PASS std.ascii.string indexof ignorecase`, `TEST_PASS std.ascii.string
trims space and tab`, `TEST_PASS std.ascii.string trim uses shared
space-or-tab helper`, `TEST_PASS std.ascii.string trims trailing space`, and
`TEST_PASS std.ascii.string compatibility boundary`.

`tests/OS.HostTests/StdAsciiTokenSplitterTests.cs` covers caller-owned token
splitting over ASCII byte spans, empty-field preservation, ASCII whitespace
trimming, empty-entry removal, combined trim/remove-empty behavior, all-empty
zero-token success, destination-capacity reporting without partial writes,
invalid-range rejection, non-ASCII/control input rejection, invalid-delimiter
and invalid-option rejection, and default-result rejection. It emits
`TEST_PASS std.ascii tokens splits fields`, `TEST_PASS std.ascii tokens
preserves empty fields`, `TEST_PASS std.ascii tokens trims fields`,
`TEST_PASS std.ascii tokens removes empty fields`, `TEST_PASS std.ascii tokens
trims then removes empty fields`, `TEST_PASS std.ascii tokens removes all empty
fields`, `TEST_PASS std.ascii tokens reports destination capacity`,
`TEST_PASS std.ascii tokens rejects invalid ranges`, `TEST_PASS std.ascii
tokens rejects non ascii input`, and `TEST_PASS std.ascii tokens compatibility
boundary`.

`tests/OS.HostTests/StdAsciiKeyValueSplitterTests.cs` covers caller-owned
key/value splitting over ASCII byte spans, first-delimiter-only splitting,
additional delimiter preservation inside the value, range-scoped source-backed
metadata, ASCII whitespace trimming, empty-value success, empty-key rejection,
missing-delimiter rejection, invalid-range rejection, invalid-delimiter and
invalid-option rejection, non-ASCII/control input rejection, default-result
rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii keyvalue splits first delimiter`, `TEST_PASS std.ascii keyvalue
preserves delimiter in value`, `TEST_PASS std.ascii keyvalue trims key and
value`, `TEST_PASS std.ascii keyvalue allows empty value`, `TEST_PASS std.ascii
keyvalue rejects empty key`, `TEST_PASS std.ascii keyvalue rejects missing
delimiter`, `TEST_PASS std.ascii keyvalue rejects invalid ranges`, `TEST_PASS
std.ascii keyvalue rejects invalid delimiter and options`, `TEST_PASS std.ascii
keyvalue rejects non ascii input`, and `TEST_PASS std.ascii keyvalue
compatibility boundary`.

`OS.Manifest.NativeManifestParser` now uses `AsciiKeyValueSplitter` for
kernel-facing manifest line parsing. Host tests verify that manifest values keep
additional `=` bytes inside source-backed values and that the parser source
depends on the shared splitter instead of the previous local token-split path.
This preserves the existing manifest format and error status behavior while
moving the common first-delimiter parsing primitive into `std/no-runtime/shared`.

`tests/OS.HostTests/StdAsciiTextBuilderTests.cs` covers caller-owned ASCII text
builder appends for ASCII byte spans, single bytes, ASCII chars, CR/LF, decimal
`ulong`/`long`, padded prefixed hex, overlap-safe byte appends through the shared
buffer operation helper, too-small destinations without writes, non-ASCII and
NUL rejection, invalid offsets, invalid hex digit widths, default/zero result
rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii.builder appends bytes and chars`, `TEST_PASS std.ascii.builder
appends decimal and hex`, `TEST_PASS std.ascii.builder rejects capacity and
input`, and `TEST_PASS std.ascii.builder default result is not success`.
`tests/OS.HostTests/PanicTextBufferTests.cs` emits `TEST_PASS panic text buffer
uses shared ascii text builder` to guard the panic-path numeric formatter
against reintroducing diagnostics-local decimal or hex loops.
`tests/OS.HostTests/NativeAppExitStatusTests.cs` emits `TEST_PASS
appmodel.exit status formatter uses shared ascii text builder` to guard the
native app `APP_EXIT` marker formatter against reintroducing app-model-local
decimal counting or writing loops.
`tests/OS.HostTests/NativeAppExitLogObserverTests.cs` emits `TEST_PASS
appmodel.exit observer formatter uses shared ascii text builder` to guard the
native app exit observer diagnostic formatter against reintroducing
app-model-local decimal counting or writing loops.
`tests/OS.HostTests/NativeManifestParserTests.cs` emits `TEST_PASS manifest env
formatter uses shared ascii text builder` to guard the manifest environment
diagnostic formatter against reintroducing manifest-local decimal counting or
writing loops.
`tests/OS.HostTests/NativeAppPackageStagingFormatterTests.cs` emits `TEST_PASS
appmodel package staging formatter uses shared ascii text builder` to guard the
package-staging readiness diagnostic formatter against reintroducing
app-model-local decimal counting or writing loops.
`tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs` emits `TEST_PASS
appmodel args vector formatter uses shared ascii text builder` to guard the
argument-vector diagnostic formatter against reintroducing app-model-local
decimal counting or writing loops.

`tests/OS.HostTests/StdAsciiDecimalParserTests.cs` covers unsigned ASCII
decimal parsing for `uint` and `ulong`, signed ASCII decimal parsing for `int`
and `long`, including zero, leading zeros, minimum and maximum signed values,
maximum unsigned values, empty input, sign-only rejection, `+` rejection,
whitespace/non-digit rejection, overflow detection, `TryParse` failure value
clearing, use of the shared `Ascii.IsDigit` classifier, default/zero result rejection, and no-managed-reference result
layout. It emits `TEST_PASS std.ascii.decimal parses uint32 values`,
`TEST_PASS std.ascii.decimal parses uint64 values`, `TEST_PASS
std.ascii.decimal parses int32 values`, `TEST_PASS std.ascii.decimal parses
int64 values`, `TEST_PASS std.ascii.decimal rejects invalid input`, `TEST_PASS
std.ascii.decimal rejects signed invalid input`, `TEST_PASS std.ascii.decimal
uses shared digit helper`, and `TEST_PASS std.ascii.decimal default result is not
success`.

`tests/OS.HostTests/StdAsciiHexParserTests.cs` covers unsigned ASCII
hexadecimal parsing for `uint` and `ulong`, including zero, leading zeros,
mixed uppercase/lowercase digits, maximum values, optional `0x` and `0X`
prefixes, empty input, prefix-only rejection, sign/whitespace/non-hex
rejection, overflow detection, `TryParse` failure value clearing,
default/zero result rejection, and no-managed-reference result layout. It emits
`TEST_PASS std.ascii.hex parses uint32 values`, `TEST_PASS std.ascii.hex parses
uint64 values`, `TEST_PASS std.ascii.hex accepts optional 0x prefix`,
`TEST_PASS std.ascii.hex rejects invalid input`, `TEST_PASS std.ascii.hex
rejects overflow`, `TEST_PASS std.ascii.hex default result is not success`, and
`TEST_PASS std.ascii.hex layouts contain no managed references`.

`tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs` covers ASCII decimal
formatting for `int`, `long`, `uint`, and `ulong`, including zero, maximum
values, signed minimum values, larger destinations, short destinations without
writes, required byte count reporting, public digit-count helpers,
default/zero result rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii.decimal.formatter formats uint32 values`, `TEST_PASS
std.ascii.decimal.formatter formats uint64 values`, `TEST_PASS
std.ascii.decimal.formatter formats int32 values`, `TEST_PASS
std.ascii.decimal.formatter formats int64 values`, `TEST_PASS
std.ascii.decimal.formatter reports capacity`, `TEST_PASS
std.ascii.decimal.formatter reports signed capacity`, `TEST_PASS
std.ascii.decimal.formatter reports required byte counts`, and `TEST_PASS
std.ascii.decimal.formatter default result is not success`.
`tests/OS.HostTests/PalExportCatalogTests.cs` additionally emits
`TEST_PASS pal.export catalog formatter uses shared decimal formatter` to guard
the PAL export-catalog marker against reintroducing PAL-local decimal loops.

`tests/OS.HostTests/StdMemoryExtensionsTests.cs` covers byte-span sequence equality, prefix/suffix checks, byte search, byte counting, any-of byte search, span search, last byte search, last span search, empty inputs, repeated patterns, exact and larger-destination copy behavior, overlap-safe copy behavior, too-small destination rejection, no-throw `TryCopyTo` success, no-write too-small failure, overlap-safe `TryCopyTo` behavior, and compatibility status. It also covers char-span `IndexOf`, `LastIndexOf`, sequence equality, empty prefix/suffix success, prefix/suffix mismatch and too-long behavior, exact/larger copy behavior, overlap-safe `CopyTo` and `TryCopyTo`, too-small `CopyTo` rejection, no-write `TryCopyTo` failure, and empty-source copy preservation. It emits the focused markers `TEST_PASS std.memoryextensions byte span startswith endswith`, `TEST_PASS std.memoryextensions count byte values`, `TEST_PASS std.memoryextensions indexofany byte values`, `TEST_PASS std.memoryextensions trycopyto success`, `TEST_PASS std.memoryextensions trycopyto too-small`, `TEST_PASS std.memoryextensions trycopyto overlap`, `TEST_PASS std.memoryextensions char search`, `TEST_PASS std.memoryextensions char sequence prefix suffix`, `TEST_PASS std.memoryextensions char copyto`, and `TEST_PASS std.memoryextensions char trycopyto`.

`tests/OS.HostTests/StdBufferOperationsTests.cs` covers exact byte copy, larger
destination copy, overlap-forward and overlap-backward memmove behavior,
too-small destination rejection without partial writes, zero-length copy,
default/zero result rejection, and no-managed-reference result layout. It emits
`TEST_PASS std.buffer memorycopy exact bytes`, `TEST_PASS std.buffer memorycopy
overlap forward`, `TEST_PASS std.buffer memorycopy overlap backward`,
`TEST_PASS std.buffer memorycopy rejects short destination`, and `TEST_PASS
std.buffer memorycopy zero length and layout`.

`tests/OS.HostTests/StdEndianPrimitivesTests.cs` covers exact little-endian and big-endian byte order, too-short `TryRead`/`TryWrite` buffers, no-write failure behavior, and round trips for `UInt16`, `UInt32`, and `UInt64`.

`tests/OS.HostTests/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSATests.cs`
covers the M04-002 independent conformance catalog. It verifies all 25 MOSA
byte-pair projections against both `OS.Std.Binary.EndianPrimitives` and .NET
10 `System.Buffers.Binary.BinaryPrimitives`, verifies the seven `ushort`
reverse vectors and double-reverse identity, validates the frozen catalog
fingerprint, rejects missing/short catalog storage, uninitialized and malformed
vectors, out-of-range and duplicate keys, reordered entries, mismatched hashes,
unknown indexes, default-result success, and managed-reference layouts, and
guards the production source against canonical `System.*`, reference imports,
reflection, dynamic invocation, exceptions, static reference catalogs, and
host-path dependencies. Its stable host marker is
`CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5
uint16Series=7 fingerprint=E81E4451E8D999F6 ... hostConformance=true
guestIntegrated=false gate4=Fail`. The MOSA snapshot is not required at test
runtime and does not become first-party build input or CLROS evidence.

`tests/OS.HostTests/SpanOverlapMemoryCastUnsafeWidthConformanceVectorsTests.cs`
cross-checks the 16-vector M04-003 catalog against .NET 10 span copy,
`MemoryMarshal.Cast`, `Unsafe.ReadUnaligned<T>`, `Unsafe.WriteUnaligned<T>`,
and `BinaryPrimitives` behavior. It covers full, middle, empty, and rejected
slices; forward/backward/equal/zero/non-overlapping copies; divisible,
combined unaligned/remainder-truncated, and expansion casts with exact complete
output byte sequences and result counts; unsigned widths `1/2/4/8`; widening,
sign/zero extension, native-width preservation, and truncation. Each unsafe
width is asserted before conversion, and a source-hostile test proves that a
width-8 `Unsafe.ReadUnaligned<ulong>` cannot be weakened to `uint`. It also rejects
short destinations without mutation, default/uninitialized/malformed entries,
count/key/operation errors, every operation-specific invalid shape, duplicate
and reordered keys, stale fingerprints, managed-reference layouts, forbidden
production dependencies, and host-only APIs in the `NOSTDLIB_EFI` pointer
branch. Host execution tests bind every result record, expected rejected slice,
overlap direction, cast policy, raw unsigned read, scratch/output canaries and
tails, atomic failures, and all three alias pairs. Warmed catalog copy,
validation, and execution allocate zero managed bytes. Its host marker is:

```text
CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail
```

This remains the audited host/.NET 10 cross-check. A separately owned and now
accepted integration compiled the reviewed `NOSTDLIB_EFI` pointer branch into
the 59-compile-item, 72-source-authority-input EFI closure. At evidence source
head `dedb55c3ddb155336ef4c7f387cf14b533c37f09`, the guest copied the fixed
catalog once into caller-owned storage, called `ExecuteCatalog` exactly once
with 16 vectors, 16 scratch bytes, and 16 result records, audited every result,
and emitted exactly one distinct guest marker:

```text
CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

The real-QEMU serial order was `EFI_CONSOLE_READY`, the distinct M04-002 guest
marker, the marker above, then `UEFI_MEMORY_MAP_READY`. Neither
`M04_SPAN_MEMORY_CONFORMANCE_FAILED` nor any other forbidden/failure marker
appeared, and the returning profile continued to `OS_SMOKE_PASS`. The separate
host marker above deliberately remains `hostConformance=true
guestIntegrated=false`; it describes a different execution and is not rewritten
as guest evidence. `done/step919.md` preserves the exact source, tool, artifact,
log, and marker tuple.

This guest result is still one constrained fixed-catalog execution. It does not
create canonical `System.Span<T>`, `MemoryMarshal`, `Unsafe`, or
`BinaryPrimitives` behavior, and Gate 4 remains `Fail`.

The source-frozen post-reboot gate at pushed commit
`8ee0311d2a7c225b77b4efa7d86bce6831c71826`, tree
`6a599b8dd076c49fe4ba69c2b1b5c04a53fe07eb`, built the complete solution and
ran the complete host harness. The host command exited `0` after 3,089.039
seconds with 3,367 `TEST_START` lines, 3,368 `TEST_PASS` lines, zero
line-leading `TEST_FAIL`, empty stderr, exactly one ordered M04-002
start/ready/pass envelope, and terminal `TEST_PASS host.tests`. Its stdout
SHA-256 is `6F98CEE6C07F749F17DC28F9D3C797D52E4B1F0062F0DD4CC4DF078D77D51493`.
The exact audit record SHA-256 is
`84798710CCEDC985A562A062DDCA74DE5D2076F5EB8E3AEEF94906B491DE5354`.

The earlier default no-stdlib EFI publish also passed and produced SHA-256
`158432A6F1BA692561A8CA047AD4E1C1362E3549E5CA8A7EF4E7288ABECD1741`.
Its real-QEMU missing-marker run remains valid negative evidence for the
default-off profile: the serial SHA-256 was
`0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1`,
and the summary correctly reported `MISSING_MARKERS CLROS_M04_002_READY`.

The separately claimed serialized EFI integration now compiles the catalog as
the 53rd explicit no-stdlib source item inside a 66-input authority closure.
`tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
source-binds the default-false project switch, pointer-only EFI branch,
caller-owned stack allocations, marker-byte reconstruction, verifier
propagation, QEMU requirements, and failure/default-off guards. A fresh
2026-08-05 QEMU 11.0.50/OVMF run rebuilt and independently verified the
62,464-byte EFI artifact, then emitted exactly once and in order:

```text
EFI_CONSOLE_READY
CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
UEFI_MEMORY_MAP_READY
```

The runner reported `terminal=Success`, `orderedSeen=3`, `outOfOrder=0`,
`uniqueSeen=1`, `duplicateUnique=0`, `MISSING_MARKERS none`,
`FORBIDDEN_MARKER_HIT none`, and `TEST_PASS qemu.smoke`. The EFI, image, serial,
and summary SHA-256 values are respectively
`44F4A7B5E2929884869F324E998ABC5B89559707ACC4BA65412EB818B18D9CA0`,
`024E42220A5543529E4A0D5F8BD7C4052062E8A9FF9E46457D913E9AF9462CD8`,
`05EE15A753899894F86CCA0318CC724DC22671C78D12E4C7CD87E3BA2F262453`,
and `646A1AFDF8572317F71381F11EB8E6646E2DAD1236FEF03E2483DCA9B7ADBDB6`.
No `TEST_FAIL` or `M04_ENDIAN_CONFORMANCE_FAILED` line appeared.

`tests/OS.HostTests/StdCrc32Tests.cs` covers the empty-input CRC32 value, the standard ASCII `123456789` vector (`0xCBF43926`), a binary byte-range vector (`0x456CD746`), and incremental update/finalization over a split binary payload (`0x7C9CA35A`). It emits `std.crc32` markers for each scenario.

`tests/OS.HostTests/StdInternetChecksumTests.cs` covers the empty input result (`0xFFFF`), the RFC 1071 sample vector (`0x220D`), odd trailing byte padding, two- and four-segment odd-byte boundary carryover, accumulator pending-byte behavior, packet validation after checksum insertion, and accumulator reset behavior. It emits `std.internet.checksum` markers for each scenario.

`tests/OS.HostTests/StdNativeIntTests.cs` covers checked pointer-sized add/subtract, overflow/underflow reporting, power-of-two validation, align-up/align-down behavior, invalid alignment rejection, align-up overflow reporting, default/zero result rejection as success evidence, and no-managed-reference result layout. It emits `std.nativeint` markers for the focused scenarios.

`tests/OS.HostTests/StdMathTests.cs` covers the current partial integer math helper slice: signed and unsigned `Min`/`Max`, lower/inside/upper `Clamp` behavior, invalid clamp-bound rejection, `Abs` for positive/zero/negative values, and overflow rejection for `int.MinValue` and `long.MinValue`. It emits `TEST_PASS std.math min max integer helpers`, `TEST_PASS std.math clamp integer bounds`, and `TEST_PASS std.math abs overflow behavior`.

`tests/OS.HostTests/StdArrayHelpersTests.cs` covers the current partial unmanaged-span array helper slice: empty-span clear/fill behavior, full-span clear, partial-span fill, first/last/missing search over duplicate values, BCL-shaped binary-search exact matches, insertion-point complements, duplicate-match tolerance, full-span sort for empty, single-element, already-sorted, reverse, and duplicate values, status-coded range-limited `TrySort` success/invalid-range behavior, status-coded range-limited `TryBinarySearch` found/not-found/invalid-range behavior, and default-result rejection. It emits `TEST_PASS std.arrayhelpers clear fill unmanaged spans`, `TEST_PASS std.arrayhelpers indexof unmanaged spans`, `TEST_PASS std.arrayhelpers binarysearch insertion points`, `TEST_PASS std.arrayhelpers sort unmanaged spans`, `TEST_PASS std.arrayhelpers trysort range and status`, `TEST_PASS std.arrayhelpers trysort default result is not success`, `TEST_PASS std.arrayhelpers trybinarysearch found range`, `TEST_PASS std.arrayhelpers trybinarysearch notfound range`, `TEST_PASS std.arrayhelpers trybinarysearch invalid range`, and `TEST_PASS std.arrayhelpers trybinarysearch default result is not success`.

`tests/OS.HostTests/NativeAotHelperLedgerTests.cs` verifies that the helper ledger contains the required runtime helper areas, carries symbol/contract detail, evidence, required contract, allowed milestone stub behavior, current test coverage, owner subsystem, next-step text, and completion-gate applicability, leaves incomplete runtime helpers unmarked as complete, prevents EFI-only helpers from satisfying kernel/runtime completion, represents all 21 current EFI `[RuntimeExport]` symbols from `src/OS/Runtime/MinimalRuntime.cs` in exactly one ledger entry, keeps `RhpStackProbe`, the profile-bound `RhpNewFast` entry and the two SZ-array exports plus RhNewString separate from the four reverse-P/Invoke and twelve fail-fast symbols, preserves existing append-only enum values, and keeps `BootPageAllocatorMutation` evidence below the object/array/string allocation helper completion boundary. It emits `TEST_PASS runtime.nativeaot helper ledger structured columns`, `TEST_PASS runtime.nativeaot helper ledger append-only values`, and `TEST_PASS runtime.nativeaot helper ledger allocator mutation boundary`.

`tests/OS.HostTests/DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.cs`
derives the 49 current canonical `System.*` declarations and 21
`[RuntimeExport]` methods directly from `src/OS/Runtime/MinimalRuntime.cs`, then
binds their identities and observed helper bodies to the allocation-free
`OS.Std.NoRuntime` ledger. It verifies the four link-only no-op, twelve
fail-fast, one audited stack-probe, one profile-bound fixed-allocation export
and two appended SZ-array exports plus the string export
classification; stable categories and support
states; reference-free entries; switch-indexed retrieval; fail-closed unknown
and default values; absence of a canonical `System.*` namespace; and the
unchanged failing Gate 4 boundary. It emits exactly one
`CLROS_M04_001_READY declarations=49 runtimeExports=21 linkOnlyNoOp=4
failFast=12 auditedStackProbe=1 gate4=Fail canonicalSystemNamespace=false`
marker between `TEST_START m04.001` and `TEST_PASS m04.001`.

`tests/OS.HostTests/BootPageFreeListPlanTests.cs` covers the caller-owned boot page free-list node construction prerequisite for future allocation work. It emits `TEST_PASS memory.boot freelist nodes constructs`, `TEST_PASS memory.boot freelist nodes rejects failed planner`, `TEST_PASS memory.boot freelist nodes reports destination capacity`, `TEST_PASS memory.boot freelist nodes rejects invalid segment shape`, and `TEST_PASS memory.boot freelist nodes rejects arithmetic overflow`.

`tests/OS.HostTests/BootPageAllocatorStateTests.cs` covers the host-tested allocator state metadata prerequisite after free-list node construction. It emits `TEST_PASS memory.boot allocator state initializes`, `TEST_PASS memory.boot allocator state rejects failed free-list plan`, `TEST_PASS memory.boot allocator state rejects empty free-list`, `TEST_PASS memory.boot allocator state rejects source capacity`, `TEST_PASS memory.boot allocator state rejects corrupted node shape`, `TEST_PASS memory.boot allocator state rejects invalid chain`, `TEST_PASS memory.boot allocator state default result is not success`, and `TEST_PASS memory.boot allocator state layouts contain no managed references`.

`tests/OS.HostTests/BootPageAllocatorMutationTests.cs` covers caller-owned
free-list mutation after allocator-state initialization. It proves allocation,
aligned splitting, free/coalescing, free-into-empty-list behavior, invalid
mutation rejection, default-result rejection, no-managed-reference result
layout, and the explicit boundary constants that keep the slice below live
allocation, paging, and NX/W^X enforcement. It emits `TEST_PASS memory.boot
allocator mutates allocation`, `TEST_PASS memory.boot allocator splits aligned
range`, `TEST_PASS memory.boot allocator frees and coalesces`, `TEST_PASS
memory.boot allocator frees empty list`, `TEST_PASS memory.boot allocator
rejects invalid mutation`, `TEST_PASS memory.boot allocator mutation default
result is not success`, and `TEST_PASS memory.boot allocator mutation layouts
contain no managed references`.

`tests/OS.HostTests/BootPageAllocatorReadinessComposerTests.cs` covers the host-tested aggregate boundary from compact boot memory-region records through allocator-state initialization and `BootSequence.RunBootPageAllocatorReadiness`. It emits `TEST_PASS memory.boot allocator readiness composes`, `TEST_PASS memory.boot allocator readiness reports scratch capacity`, `TEST_PASS memory.boot allocator readiness rejects invalid regions`, and `TEST_PASS memory.boot allocator readiness emits boot marker metadata`.

`tests/OS.HostTests/NativeAotHeapBootstrapPlanTests.cs` covers the host-tested heap-bootstrap planning boundary after allocator-state initialization. It proves a requested future heap page budget, object alignment, remaining free-page metadata, allocator byte-count consistency, no-managed-reference result layout, default-result rejection, and the `BootSequence.RunNativeAotHeapBootstrapReadiness` marker boundary while keeping `SatisfiesObjectAllocationHelpers` false. It emits `TEST_PASS runtime.nativeaot heap bootstrap plans page budget`, `TEST_PASS runtime.nativeaot heap bootstrap rejects allocator state`, `TEST_PASS runtime.nativeaot heap bootstrap rejects allocator byte metadata`, `TEST_PASS runtime.nativeaot heap bootstrap rejects page budget`, `TEST_PASS runtime.nativeaot heap bootstrap rejects object alignment`, `TEST_PASS runtime.nativeaot heap bootstrap layout contains no managed references`, `TEST_PASS runtime.nativeaot heap bootstrap default result is not success`, `TEST_PASS boot.sequence nativeaot heap bootstrap ready`, `TEST_PASS boot.sequence nativeaot heap bootstrap rejects failed plan`, `TEST_PASS boot.sequence nativeaot heap bootstrap blocks default result`, and `TEST_PASS boot.sequence nativeaot heap bootstrap preserves default markers`.

`tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs` covers the
host-tested class-constructor state-table prerequisite. It proves
allocation-free registration and state transitions over caller-owned storage,
numeric module/type key lookup, reentry-cycle detection, failed-cctor
preservation, duplicate/missing/capacity rejection, default-result rejection,
and no-managed-reference value layouts. It emits `TEST_PASS runtime.cctor state
begins and completes`, `TEST_PASS runtime.cctor state detects reentry cycle`,
`TEST_PASS runtime.cctor state preserves failure`, `TEST_PASS runtime.cctor
state rejects duplicate keys`, `TEST_PASS runtime.cctor state default result is
not success`, and `TEST_PASS runtime.cctor state layouts contain no managed
references`. The same test file also covers caller-owned diagnostic formatting
for initialized state-table results. It emits `TEST_PASS runtime.cctor state
formatter formats success marker`, `TEST_PASS runtime.cctor state formatter
reports short destination`, and `TEST_PASS runtime.cctor state formatter
rejects default result`. It also emits `TEST_PASS runtime.cctor state formatter
uses shared ascii text builder` to guard the `NATIVEAOT_CCTOR_STATE` formatter
against reintroducing formatter-local decimal counting, direct digit writes, or
direct byte-copy loops.

The same file covers the future class-constructor runner preflight contract. It
proves ready metadata, default/uninitialized rejection, failed constructor state
rejection, pending/reentrant state rejection, missing and mismatched target
metadata rejection, caller-owned diagnostic formatting, short-destination
reporting without partial writes, default-result rejection, and primitive-only
result layouts. It emits `TEST_PASS runtime.cctor runner preflight ready`,
`TEST_PASS runtime.cctor runner preflight rejects default table`, `TEST_PASS
runtime.cctor runner preflight rejects failed state`, `TEST_PASS runtime.cctor
runner preflight rejects pending state`, `TEST_PASS runtime.cctor runner
preflight rejects missing metadata`, and `TEST_PASS runtime.cctor runner
preflight formatter reports short destination`. It also emits `TEST_PASS
runtime.cctor runner preflight formatter uses shared ascii text builder` to
guard the preflight formatter against reintroducing formatter-local decimal
counting, direct digit writes, or direct byte-copy loops. It also covers the
managed boot-readiness marker wrapper and emits `TEST_PASS boot.sequence cctor
runner preflight ready`, `TEST_PASS boot.sequence cctor runner preflight
rejects failed result`, `TEST_PASS boot.sequence cctor runner preflight rejects
default result`, and `TEST_PASS boot.sequence cctor runner preflight preserves
default markers`.

`tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs` covers the
next host-tested class-constructor runner prerequisite after preflight. It
proves caller-owned work-item planning for uninitialized entries, initialized
entry skipping, missing target-token rejection, missing metadata rejection,
destination-capacity reporting, failed/pending preflight rejection,
default-preflight rejection, caller-owned `NATIVEAOT_CCTOR_WORK_ITEMS`
diagnostic formatting, short-destination preservation, default-result
diagnostic rejection, and no-managed-reference layouts. It emits
`TEST_PASS runtime.cctor work item plans uninitialized entries`, `TEST_PASS
runtime.cctor work item skips initialized entries`, `TEST_PASS runtime.cctor
work item rejects missing target token`, `TEST_PASS runtime.cctor work item
rejects missing target metadata`, `TEST_PASS runtime.cctor work item reports
destination capacity`, `TEST_PASS runtime.cctor work item rejects failed
preflight`, `TEST_PASS runtime.cctor work item rejects pending preflight`,
`TEST_PASS runtime.cctor work item rejects default preflight`, `TEST_PASS
runtime.cctor work item formatter formats success marker`, `TEST_PASS
runtime.cctor work item formatter formats failure marker`, `TEST_PASS
runtime.cctor work item formatter reports short destination`, `TEST_PASS
runtime.cctor work item formatter rejects default result`, `TEST_PASS
runtime.cctor work item formatter uses shared ascii text builder`, `TEST_PASS
boot.sequence cctor work item ready`, `TEST_PASS boot.sequence cctor work item
rejects failed result`, `TEST_PASS boot.sequence cctor work item rejects default
result`, `TEST_PASS boot.sequence cctor work item preserves default markers`,
and `TEST_PASS runtime.cctor work item layouts contain no managed references`.

`tests/OS.HostTests/NativeAppPayloadMappingPlanTests.cs` covers the current host-tested native app payload range planner. It proves that a selected `OSAPPv0` payload descriptor can be reduced to deterministic executable-content and entrypoint offsets without allocating executable memory or invoking app code.

`tests/OS.HostTests/NativeAppExecutableMemoryPlanTests.cs` covers the current host-tested native app executable-memory permission planner. It proves that a payload range plan can be reduced to page-aligned write-then-execute mapping requirements without allocating pages, changing live protections, or invoking app code.

`tests/OS.HostTests/NativeAppInvocationPlanTests.cs` covers the current host-tested native app invocation planning contract. It proves that executable-memory planning and launch metadata can be reduced to deterministic stack, entry-offset, and bounded manifest metadata requirements without allocating stacks, switching stacks, creating threads, mapping pages, or invoking app code.

## Known Limitations

M01-034 adds the minimum no-stdlib binding metadata needed to retain and name
one host-generated native helper in the EFI image:

- `System.Runtime.RuntimeImportAttribute` stores only the module and entry-point
  metadata consumed by NativeAOT;
- `System.Runtime.CompilerServices.MethodImplAttribute` and the exact
  `InternalCall` option provide only the method-implementation metadata needed
  by this binding; and
- `OS.Boot.X64InstructionPublication.Serialize()` is an `extern` binding to
  `CLROS_X64_SerializeInstructionPublication` and has no managed fallback.

These shims are deliberately narrower than the public BCL types. They do not
implement reflection behavior, metadata discovery APIs, runtime invocation,
exception behavior, or general internal-call resolution. The host integration
test decodes the freshly built managed assembly and requires the exact
no-body `RuntimeImport`/`InternalCall` metadata. The canonical EFI artifact
verifier independently binds that managed-assembly hash to the same build and
proves that the final PE contains the expected nonwritable executable bytes
plus matching nonwritable unwind coverage. M01-034 does not call the binding,
prove its success cookie in a guest, implement NativeAOT runtime helpers, or
advance a runtime or boot completion gate.

Most solution projects reference normal `net11.0` libraries to keep host
contracts buildable. `src/OS/OS.csproj` is included in `CLROS.slnx` as explicit
no-stdlib work under stricter directory-local rules; it produces a minimal EFI
PE artifact, but that artifact is not yet a boot-proven kernel. Canonical
`System.*` replacement types must not be imported into the normal host build
path.

The current `OS.Std.MemoryExtensions` type intentionally lives under `OS.Std`, not `System`, because only partial byte-span and char-span subsets are implemented. Do not expose it as canonical `System.MemoryExtensions` until the signatures and behavior are compatible with the public BCL contract and documented as such. The `CopyTo` helper follows the BCL-shaped throwing contract in the host-testable subset; kernel-facing callers that cannot depend on exceptions should use the OS-specific `TryCopyTo` helper so too-small destinations are reported as `false` without modifying the destination.

The current `OS.Std.Text.AsciiString` type intentionally lives under
`OS.Std.Text`, not `System.String`, because it is an OS-specific byte-span
helper for protocol-style ASCII text. It does not allocate strings, does not
perform culture-sensitive comparison, does not Unicode-fold non-ASCII bytes,
and does not implement substring, split, join, concat, collation, or
normalization behavior.

The current `OS.Std.Text.AsciiTokenSplitter` type intentionally lives under
`OS.Std.Text`, not `System.String`, because it is an OS-specific byte-span
tokenizer for fixed ASCII manifests and protocols. It does not allocate
strings, decode UTF-8, quote/unescape values, normalize paths, or parse nested
formats. Its trim/remove-empty options are ASCII-only convenience behavior for
caller-owned token ranges and are not a `System.String.Split` compatibility
claim.

The current `OS.Std.Text.AsciiTextBuilder` type intentionally lives under
`OS.Std.Text`, not `System.Text.StringBuilder`, because it is an OS-specific
caller-owned byte-buffer formatter. It does not allocate storage, grow buffers,
encode Unicode, format floating-point values, format culture-specific text, or
provide the full mutable-object `StringBuilder` API. It is intended for panic,
serial, protocol, and manifest formatting paths that must know required byte
counts and avoid hidden allocation.

The current `OS.Std.Text.AsciiDecimalParser` type intentionally lives under
`OS.Std.Text`, not `System.Int32`, `System.Int64`, `System.UInt32`,
`System.UInt64`, or `System.Number`, because it is an OS-specific byte-span
parser for ASCII decimal text. The signed methods accept only a leading `-`;
there is no leading `+` convention, so `+` is rejected for both signed and
unsigned methods. It does not accept whitespace, thousands separators,
hexadecimal input, culture-specific digits, Unicode digits, floating-point
values, or partial token parsing.

The current `OS.Std.Text.AsciiHexParser` type intentionally lives under
`OS.Std.Text`, not `System.UInt32`, `System.UInt64`, or `System.Number`,
because it is an OS-specific byte-span parser for ASCII hexadecimal text. It
accepts only unsigned values and an optional `0x` or `0X` prefix. It does not
accept signs, whitespace, thousands separators, decimal-only culture-specific
digits, Unicode digits, floating-point values, or partial token parsing.
The pointer partial supports only the unsigned64-bit boolean/out overload;
its borrowed-memory lifetime and zero-on-failure contract differ from the
span overloads' status results. It does not verify arbitrary address readability.

The current `OS.Std.Text.AsciiDecimalFormatter` type intentionally lives under
`OS.Std.Text`, not `System.UInt32`, `System.UInt64`, `System.Number`, or
`System.ISpanFormattable`, because it is an OS-specific byte-span formatter for
ASCII decimal text. It does not allocate strings, accept format strings, pad
output, format hexadecimal/floating-point values, or perform culture-specific
formatting.

The current `OS.Std.Binary.EndianPrimitives` type intentionally lives under `OS.Std.Binary`, not `System.Buffers.Binary`, because it is an OS-specific no-runtime helper. Direct read/write methods require spans of the correct minimum length; storage and network parsers should use the `TryRead`/`TryWrite` variants when malformed input must be status-coded instead of exception-driven.

The M04-002 independent conformance catalog is test-vector and catalog-integrity
evidence only. It does not add signed integer overloads, 128-bit or native-int
overloads, a canonical `System.Buffers.Binary.BinaryPrimitives` surface,
runtime helper behavior, or a general BCL compatibility claim. The host marker
retains `hostConformance=true guestIntegrated=false` because it reports the
host comparison itself. The separate EFI marker's `hostConformance=false
guestIntegrated=true` proves only that the frozen caller-owned catalog copied
and validated inside the no-stdlib guest; it does not promote that fixed-vector
check into general runtime or BCL compatibility evidence.

The current `OS.Std.Hash.Crc32` type intentionally lives under `OS.Std.Hash`, not a canonical `System.*` namespace, because .NET does not expose this exact no-runtime contract as a BCL compatibility surface. It uses a compact bitwise loop rather than a static table so it can migrate toward freestanding NativeAOT without lazy static reference fields. Callers that need maximum throughput can add a separately reviewed table-driven variant later only after static-data and boot-time initialization rules are documented.

The current `OS.Std.Net.InternetChecksum` type intentionally lives under `OS.Std.Net`, not a canonical `System.*` namespace, because the public BCL does not expose this exact packet-checksum contract. It is a compact shared primitive for IPv4, ICMP, UDP, and TCP checksum work. This slice does not migrate existing packet code, does not add pseudo-header builders, and does not provide vectorized or table-driven acceleration.

The current `OS.Std.NativeInt` type intentionally lives under `OS.Std`, not a canonical `System.*` namespace, because it is an OS-specific status-coded helper for no-runtime pointer-sized arithmetic. It is not a replacement for `System.IntPtr`, `System.UIntPtr`, checked operators, or runtime pointer semantics. It does not expose signed arithmetic, pointer dereferencing, unsafe memory access, or page-table mutation.

The current `OS.Std.Math` type intentionally lives under `OS.Std`, not `System`, because only a partial integer subset is implemented. It does not expose floating-point helpers, decimal helpers, trigonometry, rounding, unsigned native integer overloads, or a complete `System.Math` / `System.MathF` compatibility surface. The supported throwing behavior is host-tested, but kernel paths that cannot rely on managed exception handling should not use throwing `Clamp` or overflow `Abs` paths until exception support is complete or status-coded alternatives are added.

The current `OS.Std.ArrayHelpers` type intentionally lives under `OS.Std`, not `System`, because only a partial unmanaged-span subset is implemented. It is not a replacement for `System.Array`, does not accept managed-reference element types, does not provide array allocation, resize, covariance, comparer-based search/sort, stable-sort guarantees beyond the current insertion-sort implementation, or multidimensional-array behavior, and does not validate whether binary-search inputs are sorted. `TrySort` and `TryBinarySearch` are OS-specific result metadata for no-throw range validation rather than BCL APIs.

The current `OS.Std.Memory.BufferOperations` type intentionally lives under
`OS.Std.Memory`, not `System.Buffer`, because only a partial status-coded
byte-copy subset is implemented. It does not provide `BlockCopy`, array
covariance handling, object-array behavior, unsafe pointer overloads, or a
complete BCL buffer contract.

The current EFI shell stubs are deliberately narrow and should not be generalized into kernel runtime policy without a matching design/test step. In particular, the shell type-cast helpers are fail-fast placeholders, not a correct interface-dispatch or exception implementation.

`NativeAppExecutableMemoryPlan` is not a NativeAOT runtime helper implementation. It documents future page layout and W^X policy for native app payload bytes only; it does not implement allocation helpers, write barriers, unwind support, GC root scanning, page-table updates, or instruction-cache maintenance.

`BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`,
`BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, and
`NativeAotHeapBootstrapPlanner` are not NativeAOT runtime helper
implementations. They write, validate, mutate, or compose caller-owned metadata
from a validated boot memory plan and can plan a future managed-heap page budget
from allocator-state metadata, but they do not allocate objects or arrays,
reserve live pages, register GC roots, run write barriers, install a managed
heap, mutate allocator-owned live state, or run in the live EFI path.

`NativeAotClassConstructorStateTable` and
`NativeAotClassConstructorRunnerPreflight` and
`NativeAotClassConstructorWorkItemPlanner` are not a full NativeAOT
class-constructor runner. They do not discover metadata, execute constructors,
call delegates, run module initializers, manage lazy static reference fields,
unwind exceptions, diagnose constructor bodies, or integrate with boot/runtime
helper exports. They only track caller-owned value-type state transitions,
validate already-supplied preflight metadata, turn already-supplied target
tokens into caller-owned work-item metadata, and format already-produced state,
preflight, and work-item plan results for future runner diagnostics.

`NativeAppInvocationPlanner` is not a NativeAOT runtime helper implementation. It documents host-tested metadata prerequisites for a future native app launcher only; it does not allocate stacks, switch stacks, create threads, define the final app ABI, map pages, relocate code, or invoke app code.

## Next Steps

1. Add host-safe `OS.Std.*` string, memory query, and remaining integer helper overloads where they do not collide with CoreLib.
2. Move all shared utilities into `std/no-runtime/shared` before use by kernel or apps.
3. Keep compatibility ledgers source-bound and append-only as `System.*`
   declarations or runtime exports change; do not promote a classification to
   Gate 4 until the underlying behavior has matching host and guest evidence.
4. Keep the independent endian catalog fingerprint and append-only vector keys
   frozen; add new width/signedness families as separate reviewed slices rather
   than changing the M04-002 vector identities.
5. Migrate IPv4, ICMP, UDP, and TCP checksum paths onto `OS.Std.Net.InternetChecksum` where it reduces duplicated packet-local checksum loops.
6. Replace shell-only runtime placeholders with tested runtime helpers as the boot path starts using them.
7. Preserve the accepted retained-heap, root and write-barrier authority when
   extending allocation or collection beyond their bounded profiles.
8. Add helper-specific QEMU probes before changing any panic-stubbed helper to successful behavior.

### M04-010 returning-helper integration checkpoint

[Step973](../done/step973.md) connects the original retained-page contract to a
default-off, noncollecting reference probe. The generated `RhpAssignRef`,
`RhpCheckedAssignRef` and `RhpByRefAssignRef` bridges call the bounded scalar
callback; `System.Buffer.BulkMoveWithWriteBarrier` uses the same POD admission core through
the explicit managed route. Six allocation roles, original authority/cursor
state, full-prefix exclusion, Pair completion and one bounded block move are
checked. Unsupported destinations, roles and spans remain fail-closed.

The complete focused managed batch passes 82 starts/83 passes/zero failures;
the complete reference-EFI initializer passes 5 starts/6 passes/zero failures,
including its actual mapped core child and fresh default/enabled publications.
The earlier host-only contract and general WriteBarriers ledger boundary remain
distinct. [Step977](../done/step977.md) records the accepted `eeee523` common
suite and fresh reference capture: six genuine allocation roles, 23 callback
events and five operation records in the strict 70-record body. Fresh managed
metadata and whole-image/method/relocation joins bind the reviewed special-helper,
callback and lower-128-bit XMM/MXCSR analysis to this image; production-contract
numeric replay is not an independent numeric oracle.

The step977 image has static whole-call bounds of 9,456 bytes for special-helper
routes and 9,072 for bulk, within the 135,168-byte owned stack. That capture
observed initial RSP containment and static-context guards, without an actual
executing-stack sentinel check.

[Step979](../done/step979.md) adds two sentinel words at the bottom of that
original usable stack. A conditional getter validates the original private M01
state and retained switch plan; a real local-address witness must leave at least
16 KiB above the sentinel strip. A separate 96-byte POD state records initial
reads and five later checks, after callback counts 18, 19, 20, 22 and 23. The
reader is non-inlined, corruption is terminal, and the final check follows full
retained-page verification before the new STACK record and READY.

The complete reference initializer passes 5 starts/6 passes, including mapped
controls, strict 71-record transcript/streamer tests and both actual NativeAOT
publications. [Step981](../done/step981.md) joins the fresh `5d894c8` images to
the compiled reader, stores and five two-load checks: the witness is entryRSP
minus 72, exact returning depth is 8,448 bytes and the conservative local bound
is 8,456 bytes, within the 16 KiB admission ceiling. The clean common suite passes
3,704 starts/3,705 passes/zero failures and 16 READY lines. Fresh default,
reference and ACPI captures pass separate native, raw/lifecycle and disk
reviews; the reference has 71 records and two unchanged words through all five
checks, while ACPI retains 35 records. This accepts the bounded #79/#537 profile;
PR540 subsequently merged at `f3b4c83`; #79/#537 closed with evidence and
#80 was claimed for the next metadata slice.
The witness is not a sampled RSP, and sentinel readback does not measure peak
usage or arbitrary firmware/interrupt stack safety. No collecting GC,
wider-vector preservation, unwind support, real ExitBootServices or hosted
runtime is claimed; Gates3/4 remain **Fail**.

### M04-011 one-module metadata registration

[Step983](../done/step983.md) adds
`TypeManagerModuleMetadataLiveNativeAOTImage`: an allocation-free, status-coded
`Validate` / `RegisterOnce` / `TryGetSection` contract using the original
`EfiLoadedImageRangeCapturePlan` and typed M01 identity mapping. A single bounded
loaded-PE/.modules/RTR decoder admits one relocated .NET 10.0.8 RTR 16.0 module.
It rejects unsupported dehydration/multiple modules, malformed or overflowing
spans, overlapping PE sections, W+X, unsupported row flags and slot/storage
aliases before writes.

Caller-owned retained storage keeps the private owner state separate from the
48-byte native TypeManager ABI. Registration checks the complete storage
capacity and all 16 writable slot bytes, captures exact header/section-table,
.modules and RTR bytes, writes the ABI and index 0, then publishes the handle
last. Lookup revalidates original capture, owner, capacity, handle, ABI, slot and
exact metadata; a structurally valid metadata replacement is rejected. Missing
sections differ from present empty sections; row payloads remain opaque.

All five focused initializers pass 81 starts/82 passes/zero failures, including
the new 15-case module. Actual host layouts are ABI 48/private state 272/result 40/
slot 16. The current explicit EFI source closure plus the new core compiles as
an 89-source no-stdlib library with zero AssemblyRefs. The normal HostTests
Release build also passes. The first focused build failed because its ignored
harness omitted an existing helper-source dependency; the retry linked that
unchanged source without invoking its initializer or changing product code.

Step983 is host and managed-compilation evidence only. The subsequent step989
acceptance discovers and registers the actual current image and exercises a
slot-derived consumer in QEMU. The upstream runtime registry, GC/static
initialization, TLS, cctors, roots, collection, concurrency and hardware-fault
rollback are not implemented by this contract.
Callers retain actual mapping and lifetime obligations; numeric admission is
not a mapping probe. `TypeManagerRtrScans` remains Deferred and Gates3/4 remain
**Fail**; #81/#82 and hosted .NET/Kestrel remain unaccepted.

[Step985](../done/step985.md) adds the default-off EFI adapter for this contract.
It uses the original M01 capture and typed mapping, fixed 4096-byte storage,
the actual slot-derived handle and all six qwords of the separate ABI. Its
controlled mapped tests exercise the adapter and consumer, including rejected
short/repeated/foreign registration and the internal post-registration
canaries. The exact reviewed core enters the explicit no-stdlib source closure
without relaxing generic source guards.

The full eleven-module managed batch passes 194 starts/195 passes/zero failures;
the complete five-group native integration initializer passes 5 starts/6 passes
with actual default/enabled publications. Both builds and the normal HostTests
Release build succeed. These focused checks are followed by the independently
reviewed [step989](../done/step989.md) milestone: full common validation at
`34bd75c` passes 3733 starts/3734 passes/zero failures, and fresh default and
TypeManager QEMU captures pass the raw/lifecycle/provenance, native association
and disk reviews. All seven complete native files per profile match the
accepted emitted-code proof. The enabled image uses 1824 bytes of its 4096-byte
owner buffer, one RTR16.0 module with 31 rows, a separate 48-byte ABI and the
full 16-byte slot; the real slot-derived consumer reads all six ABI words.

The accepted static stack bound is 16159 bytes including the entry allowance,
within the 135168-byte usable owned stack; firmware/interrupt depth is excluded.
This is emitted-code analysis, not an observed guest stack-top/canary number.
The enabled transcript contains 45 ordered unique records and immediate EOF;
its tail canaries are initialized after registration. These finite results
complete #544/#80 and unblock #81, while #82 still depends on GC-static roots.
The runtime ledger stays Deferred and the preceding unsupported-feature
boundaries remain unchanged.

### M04-020 x64 unwind metadata prerequisite

`X64PERuntimeFunctionUnwindMetadataHostileInput` validates and snapshots one
retained module's PE runtime-function directory and decoded unwind prefixes.
Both absolute-PC `Lookup` and indexed `LookupFunction` revalidate the original
registration and complete snapshot before selecting a listed function. Indexed
misses return zero payload only after that validation. The shared implementation
adds no second parser or retained state; [step1060](../done/step1060.md) records
the nine original host groups with indexed bounds, identity and allocation
checks. Indexed access permits a later EH decoder to check nearest-root grouping;
it does not itself establish handler ownership. Admission is bounded to 2,048
functions and 32 chain links, version
one records and the documented same-image listed-chain/save-only secondary
subset. Language-specific handler data has only a reported start, not a generic
admitted payload length. A missing function row does not establish a safe leaf.

The nine focused core groups in [step1055](../done/step1055.md) are host evidence.
The default-off #574 adapter reuses the original module, invokes actual sample
methods and passes a development native publication and ordinary 67-record QEMU
profile in [step1057](../done/step1057.md), before the indexed API change. That
older image is not acceptance evidence for the changed source. The decoder's bounded stack buffer
requires the existing no-CRT fixed RX security-cookie anchor; it is never
called, and randomized initialization remains separate hardening work.
[Step1071](../done/step1071.md) accepts the current indexed decoder at `4625fd7`
after the original full host gate and fresh default/TypeManager/unwind captures.
All 298 current runtime-function rows reproduce the 7,412-byte snapshot, and
all 80 reachable unwind-profile methods pass current native/ABI/stack review.
The bound is 7,440 bytes below module entry and 8,768 below its continuation,
excluding opaque firmware callback bodies. Exact four-success/two-miss lookup
observations, original ownership and both stack sentinels are retained.
General stack walking, restored registers, GC stack scanning, handler search,
exception objects, catch/finally and rethrow remain outside this parser and
belong to M04-021. The general exception/unwind ledger remains deferred.

### M04-021 bounded one-frame host interpreter

`X64FrameUnwind.UnwindOne` in #578 ([step1059](../done/step1059.md))
revalidates the original module and unwind snapshot, reads a caller-owned bounded
stack, and returns one POD caller context. It carries RIP/RSP, the nonvolatile
integer registers and all XMM6-XMM15 bits. It does not overlay Windows CONTEXT
or capture/restore processor registers. Every failure returns a status with a
zero payload; input context, image, metadata and stack remain unchanged.

The supported subset is version-one non-chained normal frames: integer/XMM
saves, small/large allocation and frame-pointer operations, with only executed
operations undone in a partial prolog. SAVE and SET_FPREG use the original fixed
frame base even if an earlier restore changed the candidate frame register.
This defensive admitted-shape rule does not claim full RtlVirtualUnwind
equivalence. Missing function records do not establish leaf frames.

An internal recognizer plans the remaining epilog before reading the stack:
ADD RSP or supported non-SIB LEA from the declared frame register, nonvolatile
POPs, and RET/F3 RET. The limits are 32 bytes and 16 instructions. Partial
POP/RET suffixes do not repeat prior restores; epilog results have
`InEpilog=true` and `EstablisherFrame=0`. Tail jumps, RET imm16, F2, unsupported
prefixes/addressing, truncation and exhausted limits reject without falling
back to body unwinding. Some legitimate body instructions are conservatively
unsupported. Machine/chained frames and cross-image returns are excluded.

Six focused host groups pass, including exact-end reads, late atomic failure,
unchanged backing and warmed zero allocation. Caller-supplied numeric extents
and a context do not prove mapping, compiler provenance or a live instruction
boundary; original storage and code must remain exclusively stable. Recovered
RSP advances inside the admitted stack, and raw RIP must be same-image RX.
The accepted #578 host core is merged through PR #580. This accepted scope
does not establish live stack walking, CPU transfer, handler execution,
exception roots or compiled throw/catch/finally/rethrow. #579 separately
accepts bounded host EH-record parsing; the separately accepted #582 profile adds
one real caller restoration and typed catch under its own exact image/stack
constraints ([step1075](../done/step1075.md)). General EH remains #90 work.

### M04-012 read-only GC-static metadata admission

[Step990](../done/step990.md) adds `GCStaticRootsMalformedMetadata.ValidateOne`.
Each call revalidates the registered TypeManager owner and section201 lookup,
then admits one signed field-relative entry, an aligned eight-byte cell, a
16-byte abbreviated MethodTable and one positive GCDesc series. Caller-supplied
readable windows must lie within the original image. They express mapping and
lifetime obligations, not PE write permissions or physical mapping proof.

The supported uninitialized form has tag1 and zero upper32 bits. The decoder
checks the related Object MethodTable address, HasPointers flags, aligned base
size24..4096, signed series length and pointer offsets before returning scalar
observations. Missing/empty sections, malformed spans, protected aliases and
valid unsupported forms have distinct status results. Every call authenticates
the owner again; row payload observations are fresh and are not retained
publication authority. No allocation, write, callback or static initialization
occurs. In particular, an initialized absolute cell is not accepted as an
uninitialized relative pointer.

The five complete focused initializers pass42 starts/43 passes/zero failures,
including all15 new groups, whole-buffer preservation and a warmed1000-call
zero-allocation assertion. Actual host layouts are window16/result88. The
current92 explicit EFI sources plus this core compile as a93-source no-stdlib
library with zero AssemblyRefs and a nonempty `ValidateOne` body; this managed
DLL is not executed. The normal HostTests Release compilation also passes.

This is the first host prerequisite for #81, which remains open and claimed.
Genuine compiler section201/GCDesc association, explicit cell-write permission,
sole-allocator static-base materialization, static-field barrier authority and
retained-root lifetime remain unimplemented. #82 remains blocked. There is no
new native-image, QEMU, GCHandle, collecting-GC, real ExitBootServices or hosted
runtime evidence; Gates3/4 remain **Fail**.


### M04-012 retained static-base host materialization

[Step992](../done/step992.md) implements the next #81 host slice with #548's
TypeManager range query and abbreviated-MT allocator route. ValidateOne keeps
its read-only contract. MaterializeOnce authenticates the original registered
image, supported metadata, writable cell, caller-owned root storage and all
original retained-heap inputs. It uses the existing sole allocation commit,
checks actual zero payload/MT/syncblock, completes a private retained record,
then publishes the actual allocation reference in one aligned cell store.

The private root is self/capacity-bound and retains the metadata bytes and
genuine receipt. Its local byte checksum detects accidental record changes;
it is not authentication against hostile forging or resets. A published repeat
or query revalidates the original authority, cell, metadata and receipt without
decoding the absolute published reference as rel32. Later allocations and
payload writes are legal. Pre-commit rejection preserves all storage; a
post-commit discrepancy leaves a poisoned non-retryable record without rollback.

Image-contained root/bump storage must be admitted RW/nonexecutable data and
exclude registered protected extents through its full capacity. External root
storage retains caller mapping/lifetime obligations. Backing storage must be
disjoint from the image and authority buffers. The allocator's scalar16-byte
MT route shares the existing fixed/array/String controller and sole commit;
it never reads a fabricated ordinary24-byte MethodTable prefix.

Seven whole host modules pass81 starts/82 passes/zero failures, including
existing array/String regressions. The root owner is600 bytes and result48;
1000 warmed queries allocate zero managed bytes. The actual93-source no-stdlib
child has zero AssemblyRefs and nonempty validator/materialization/query
bodies; it is not executed. Normal HostTests Release compilation passes.

This supersedes the unimplemented host-materialization items in step990;
genuine compiler GCStatics/accessor association, compiled reference stores via
the checked barrier, native ABI and fresh guest evidence remain outstanding.
#81/#548 stay open, #82 blocked, and Gates3/4 Fail. No collecting GC, general
GCHandle, concurrent/root-reset support, hosted .NET/Kestrel or cloud claim.

### GC-static EFI development milestone (step994)

The default-off `EnableM04GcStaticRootMaterializationProbe` has development
QEMU/OVMF evidence for one compiler-emitted `static object` field: register the
original image, materialize and retain its base, allocate a genuine value through
the same cursor, store/read that object, then store/read null. The existing
checked-barrier thunk performs two scalar stores; two allocations consume 48
bytes. This is bounded noncollecting support. Compatibility declarations are refreshed; final integrated
acceptance remains pending; see [step994](../done/step994.md).

NativeAOT places the section201 entry array in writable `.data`. The Type range
query's `ReadableSectionData` kind admits reads inside registered non-executable
row payloads, without granting write access. GC-root ownership retains and checks
the entry bytes. Descriptors, abbreviated method tables and Object metadata still
require immutable readable storage; mutable destinations exclude registered rows.

[Step1003](../done/step1003.md) supplies final common/native/QEMU acceptance
for this bounded GC-static slice: 3787/3788/0 complete host results, exact
native-image reuse, and fresh default/enabled captures with raw/lifecycle and
disk reviews. The actual static field/cell, relative metadata, scalar object
MethodTable and sole-allocator receipts agree with the numeric guest record.
Five owned-stack canary checks pass within the reviewed first-party bound.
#81/#548 are ready for closure after merge and #82 can proceed to collection.
This does not implement collection, free/reuse, general roots/static/TLS,
exceptions, concurrency or hosted .NET; Gates3/4 remain Fail.

### Bounded collector reference maps (step1005)

`BoundedNonMovingCollectorExplicitRootEnumeration.InspectType` now decodes
pointer-free fixed types and up to four disjoint positive GCDesc series from
registered immutable image metadata. Every read also fits the caller's explicit
readable window. The abbreviated GC-static MethodTable uses its actual 16-byte
prefix; ordinary fixed types require 24 bytes. Negative/repeating descriptors,
variable-sized types, finalizers, overlapping series and malformed ranges fail
without changing caller storage. Results are immediate observations, not retained
root or allocation capabilities.

The focused host cases and a 99-source no-stdlib compilation pass; the latter
has zero assembly references and was not executed. See [step1005](../done/step1005.md).
Issue #82 remains in progress: authoritative allocation tracking, tracing,
atomic sweep, dead-block reuse and guest acceptance are still required.

### Explicit-root collection and same-owner reuse (step1006)

The #82/#552 host implementation now traces and reclaims bounded fixed objects.
`GuestTestRhpNewFast.EnableCollection` opts in once, before the first allocation.
Its original 80-byte prefix remains unchanged; the complete collecting owner
requires 5520 bytes, including 170 private allocation records. The ordinary
fixed/static allocation path uses those records to find reusable holes. Allocation
identities increase monotonically and the observed high-water cursor never
rewinds. Swept or replaced receipts fail even when address and MethodTable match.
Existing noncollecting fixed, array, string and retained-root behavior is preserved.
Arrays and strings are not admitted in collecting mode.

`Collect` validates the original page owner, every live record, registered type
metadata and all reference fields before sweeping. Explicit roots are original
allocation receipts; default receipts mean null. One optional original GC-static
owner is revalidated through `TryGetRetainedRoot`; a tracked static base cannot
silently be omitted. A fixed queue and three mark words trace at most 170 objects.
Invalid/interior/foreign references, stale root receipts, corrupt records or
metadata, and exhausted work limits return without heap/owner writes. After
validation, an infallible commit clears dead blocks and retires their records;
the same allocation path reuses those blocks with fresh identities.

Atomic failure behavior assumes synchronous exclusive access throughout the
operation; it is not synchronization for concurrent mutators. Caller mappings,
metadata immutability, complete root enumeration and lifetimes remain obligations.
Raw field pointers carry no historical generation, so manufactured pointers after
an object's lifetime ends cannot be distinguished from a new object at that address.
No arbitrary stack walking, moving GC, finalizers or weak references are provided.

[Step1006](../done/step1006.md) records focused graph/failure/reuse cases, existing
allocator and root compatibility checks, and an updated compile-only no-stdlib
closure. EFI integration, full batch acceptance and actual guest proof remain
pending; #82/#552 stay open and Gates3/4 remain Fail.

The existing default-off GC diagnostic now also has a prepared compiler-object
collection path ([step1007](../done/step1007.md)). It binds the collecting owner
before materializing the static base, preserves the prior object/null store
checks, and then builds a real reference chain and unreachable cycle. Four
collections check reclamation, same-address reuse, stale receipts and repeated
retained-root lifetime, with five additional owned-stack canary observations.
The enabled 99-source no-stdlib closure compiles. This code has not been published
or executed in a guest; shared project admission and strict serial parsing are
still pending behind #551. The original enabled project/profile is therefore
not yet ready to run from this development branch.

[Step1008](../done/step1008.md) adds development NativeAOT/QEMU evidence for
that source at `836790c`. An isolated project compiled the same 99 original
source files with the existing nine-switch profile. The guest reached all nine
collector records and normal boot success: dead-cycle reclamation, actual
same-address reuse with a fresh identity, subsequent reclamation and a no-op
repeat all passed the diagnostic's checks. Both canaries survived ten observations.
This does not replace official source admission, strict runner/profile validation,
native association/stack review or the finished batch's common acceptance.

The diagnostic now emits `CLROS_M04_013_READY` only after those checks complete.
[Step1009](../done/step1009.md) observes it exactly once in a fresh development
QEMU capture with 70 program records and ordinary boot success. Official source
admission and strict parser integration remain pending.

[Step1011](../done/step1011.md) completes that source admission and strict parser
integration after the accepted final-map batch. The existing GC profile now
includes the collector core through 101 explicit Compile items; producer and
verifier agree on 115 raw inputs. Its original fixture accepts the complete
70-record stream and rejects missing, duplicate, reordered and altered records.
The enabled publisher preflight passes. Final acceptance is recorded below.

### Accepted bounded collection and reuse (step1041)

[Step1041](../done/step1041.md) accepts #82/#552 at source `42a516e`:
the unfiltered host suite passes (3800 starts, 3801 passes, zero failures), and
fresh ordinary default/GC QEMU captures contain 21/70 CLROS records. Four
collections preserve explicit and retained static roots, trace the compiled
child field, reclaim a dead cycle, reuse its address with a fresh identity,
reject the stale receipt, and finish with two live objects. Eight allocation
identities and a 176-byte high-water mark remain within the original 4096-byte
heap; all ten owned-stack canary observations pass.

The final EFI, link map and ILC map match the reviewed native image. Canonical
publication changes only debug-section bytes in its COFF object; all other
bytes, symbols and relocations match. Scoped caller/buffer review supplements
the existing ABI, unwind and 13688-byte first-party stack analysis (14136 with
the conservative thunk allowance, below the 16384-byte clearance). Source,
native observations, process lifecycle and boot-disk accounting agree.

This admits the finite synchronous collector and same-owner reuse. Arbitrary
stack scanning, concurrent or moving GC, general runtime initialization and
hosted .NET remain unsupported. Firmware/interrupt stack depth is excluded.
Gates3/4 remain Fail. Seeded churn and real-exit integration are the next work.

### Seeded retained-heap churn preparation (steps1035-1036)

`SeededAllocationChurnCollectionRootRetentionGuestProbes.Run` exercises the
original collecting owner with 1-64 seeded rounds and four explicit root slots.
Each round allocates 1-4 pointer-free 24-byte objects, replaces one root, collects,
and verifies payload retention, retired receipts, fresh allocation identities
and actual address reuse. Final cleanup releases the probe roots while retaining
one caller-supplied permanent receipt and the optional original GC-static base.
The initial collection may reclaim other objects; callers must supply every
external live root and keep mappings/metadata stable under exclusive access.
Initial reclamation has separate counters. Failures report partial progress;
this destructive diagnostic does not roll back earlier rounds.

`OS.Std.DeterministicRandom32.Next(ref uint)` supplies repeatable xorshift32
scheduling. A zero state selects the fixed `DefaultSeed`; the helper allocates
nothing, throws no exceptions and provides no entropy or security guarantee.
It is an OS-specific utility, not an implementation of `System.Random`.

Focused host checks cover repeatability, zero managed scratch allocation,
retained-root payloads, a sole child held through a static base, same-owner reuse,
cleanup, invalid bounds/types, stale roots and noncollecting owners. The shared
probe and utility also compile in an isolated 103-source no-stdlib closure with
zero assembly references. See [step1035](../done/step1035.md).

[Step1036](../done/step1036.md) adds a default-excluded native adapter. An isolated
104-source publication adds its call after the original collector graph in a
copied source file; the canonical project, call site and probe flags are unchanged.
On QEMU/OVMF, three 64-round seeds completed 476 allocations and reclamations,
198 collections and 312 observed address reuses while retaining the actual
compiled static field and its child payload. Eleven ordered `M04_CHURN` records,
four unchanged stack-canary checks, `CLROS_M04_014_READY` and the normal smoke
terminal marker were captured. Same-image debugger controls corrupting the low
stack canary or retained child payload both emitted failure records and entered
the existing fail-fast spin without readiness or smoke success.

[Step1037](../done/step1037.md) associates all 577 compiled methods with the exact
development PE and checks the 169-method GC/churn call graph. Conditional native
stack accounting is 14344 bytes below the seeded entry and 15272 below the full
GC entry; conservatively adding the verified 448-byte reference thunk gives
15720 bytes within the existing 16384-byte diagnostic clearance. Actual unwind
records, call alignment and nonvolatile-register restoration checks pass. The
reachable graph has no FS/GS-relative TLS access. Firmware callback bodies and
interrupt nesting are excluded. [Step1039](../done/step1039.md) completes the
scoped new-code pointer/caller review: actual module240/heap104/root88/static40/
return48 caller windows are disjoint, and native root/batch/retired/cookie buffers
cover the bounded source indices. Four existing-method deltas resolve to the
same read-only cookie; the GC entry changes only by the new call and shifted
branches. This inherits the original collector's pointer/lifetime contracts;
final canonical-image association remains required.

[Step1038](../done/step1038.md) prepares
`scripts/seeded-collector-churn-records.ps1`. Its record validator checks the eleven
ordered rows and ready marker against the preceding validated GC records: original
type/static root/child, exact seeded accounting and fingerprints, cumulative
allocation identity, retained payload, stack-witness continuity and canaries.
The caller must still validate raw framing, the original GC/heap records and
runner lifecycle. An ignored runner copy composes these checks over the complete
82-record development transcript; the actual capture passes and 26 malformed or
failed captures reject under Windows PowerShell 5.1. The canonical runner and
profile remain unchanged, so this helper is preparation for later integration.

[Step1040](../done/step1040.md) prepares the shared first-exit/churn integration:
108 explicit compile inputs, 44 profile switches, the default-off churn authority
and original collector call site, linked-adapter checks and a single shared
transcript helper. The staged ordinary churn wrapper passes with the exact
step1036 EFI and linker-map hashes, so the scoped native review can be reused.

The staged results above use allocator APIs directly and do not prove
compiler-created allocations. Stack witnesses/canaries alone do not establish a
native stack bound. #82 is accepted and closed. [Step1042](../done/step1042.md)
integrates the reviewed profiles under shared lane #560; [step1051](../done/step1051.md)
records final common and fresh guest acceptance at `e64e9dc`, including the
independently reviewed native and retained-control associations. This remains a
bounded explicit/retained-static-root collector diagnostic. Compiler cctor
execution/once/reentry (#562), full cached exception/rethrow (#84), concurrent
initialization, general GC/runtime startup and hosted .NET/Kestrel remain
unproved; Gates3/4/5 remain **Fail**.

## Bounded throw/catch accepted scope

The #658/#659/#660 contribution-catalog batch is in development on parent #90.
The host producer joins exact ILC map lengths to all original COFF root/funclet
frame endpoints, retains original input digests and checks EH payload hashes.
Code node hashes precede local fixup resolution and are not raw COFF code hashes.
Allocation-free lookup requires the reviewed original catalog extent, retained
module/image/unwind authority and complete row/EH validation. Readonly bytes do
not establish compiler provenance. [Step1182](../done/step1182.md) records focused
development checks only; the catalog-backed physical-frame guest path, combined
full-host qualification and native admission remain pending. #90 and Gate4 stay
open.

The default-off #582 child allocates a compiler-generated derived exception
through the original allocator, retains its explicit receipt root through
prethrow collection, then executes a non-inlined throw into a typed base-class
catch. Original module metadata, the one-frame interpreter and bounded type
predicate select the actual compiler funclet. The C# COFF bridge captures real
GP/XMM state, validates the returned continuation, and restores post-catch state
on the selected caller stack. [Step1075](../done/step1075.md) accepts this exact
`ead4fca` profile; [step1066](../done/step1066.md) remains its earlier development
boundary.

The same-image negative check changes only a local RIP scalar and uses the
production capture-admission predicate before any unwind/handler selection.
Live capture, roots, phase and transfer state stay unchanged. This is bounded
input rejection, not a corrupted-live-context terminal-halt result. Actual
compiled catch entry and continuation remain required by the positive path.

The profile requires measured Win64 frames, exact ILC/COFF contribution lengths,
stable original module/heap/stack authority, disabled CET and no allocation or
collection during dispatch. General finally/filter/rethrow, nested/collided
exceptions, automatic stack roots, EH-safe collection, constructor-failure
caching and general exception construction remain unsupported. The accepted
host-only #578/#579 leaf contracts are unchanged; this profile supplies only its
additional finite execution proof. #90 and Gate 4 remain incomplete.

The #589 generator checkpoint adds `--single-finally-catch` and a separate
1,707-byte COFF bridge proposal. Its host instruction models check cleanup
before catch, restoration of the selected register context after cleanup, and
preservation of the catch-produced continuation state. The legacy 1,350-byte
object remains byte-identical. [Step1079](../done/step1079.md) records this
host-only checkpoint. It does not admit a compiler finally funclet or prove
guest execution. [Step1081](../done/step1081.md) records the first disabled
native candidate: exact COFF/PE joins measure Catcher at 136 bytes and its EH
table at 11 bytes. [Step1082](../done/step1082.md) implements the disabled
selector and both validators with host-tested clause, context and control
predicates. [Step1083](../done/step1083.md) binds the implemented disabled image,
both funclets, ordinary ABI and transitive stack bounds. Its disabled selector
still had an incorrect RSP delta; [step1084](../done/step1084.md) corrects it from
16 to the measured 48 and adds exact metadata and transcript guards.
[Step1085](../done/step1085.md) completes corrected disabled-image pointer review
and permits bounded development transfer. [Step1088](../done/step1088.md) now
accepts the enabled image and fresh ordinary guest execution: one compiler
finally runs before the typed catch and compiled continuation, preserving the
original rooted exception and observing the parent-stack cleanup stamp.
Enabled-image comparison covers all 643 methods and 646 runtime rows; the
1,336 linked bridge text bytes match the reviewed object. The original full host
gate and default/GC/legacy-catch regressions pass. This finite pre-exit profile
does not provide general cleanup search, rethrow, filters, collided exceptions,
automatic stack roots, EH-safe collection or hosted .NET. #90 and Gate4 remain
incomplete.

The guarded empty `System.Exception` declaration is only the compiler-required identity for this fixture hierarchy. The canonical declaration ledger appends identity 49 as `CoreType/Stubbed`; it supplies no message, stack trace, inner exception or other exception service. All runtime-completion fields remain false.
