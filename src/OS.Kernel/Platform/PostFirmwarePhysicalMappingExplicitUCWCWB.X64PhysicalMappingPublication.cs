using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;
/// <summary>
/// First-party x64 bytes for the exclusively owned, single-CPU mapping adapter.
/// Publication ABI: uint (ulong* entry, ulong expected, ulong replacement,
/// ulong ownedCr3). The adapter must authenticate the original table/entry,
/// admit cache policy and keep all code/stack/table accesses outside the leaf
/// being removed. This emitter grants no allocation or execution authority.
/// </summary>

public static unsafe class X64PhysicalMappingPublication
{
    public const uint RequiredBytes = 164;
    public const uint GuardRequiredBytes = 174;
    public const uint ReadMsrBytes = 14;
    public const uint ReadCapabilitiesBytes = 55;
    public static uint WritePatBytes => EncodeWritePat(null, 0);

    private const ulong LowEnd = 0x0000800000000000;
    public static bool Emit(byte* destination, uint capacity)
    {
        if (Encode(null) != RequiredBytes || !Admit(destination, capacity, RequiredBytes))
            return false;
        return Encode(destination) == RequiredBytes;
    }

    public static bool ValidateReadBack(byte* code, uint capacity)
    {
        if (!Admit(code, capacity, RequiredBytes))
            return false;
        byte* expected = stackalloc byte[(int)RequiredBytes];
        if (Encode(expected) != RequiredBytes)
            return false;
        for (uint i = 0; i < RequiredBytes; i++)
            if (code[i] != expected[i])
                return false;
        return true;
    }

    /// <summary>
    /// Separate publication ABI for canonical absent guards. Exactly zero is
    /// allowed on either side; every nonzero word must be present. The trusted
    /// adapter still decodes the complete word, owns the entry/current root and
    /// keeps all executing code, stack and tables outside the affected page.
    /// Caller supplies a writable buffer of at least GuardRequiredBytes; a
    /// rejected span returns false before mutation. Emission is allocation-free
    /// and grants no execution authority. Original Emit stays present-only.
    /// </summary>

    public static bool EmitGuardPublication(byte* destination, uint capacity)
    {
        if (EncodeGuard(null) != GuardRequiredBytes || !Admit(destination, capacity, GuardRequiredBytes))
            return false;
        return EncodeGuard(destination) == GuardRequiredBytes;
    }

    /// <summary>Read-only identity check over a caller-owned readable span that
    /// stays live for this call. Returns false for rejected spans or byte changes
    /// without mutation; grants no execution or table-write authority.</summary>

    public static bool ValidateGuardPublication(byte* code, uint capacity)
    {
        if (!Admit(code, capacity, GuardRequiredBytes))
            return false;
        byte* expected = stackalloc byte[(int)GuardRequiredBytes];
        if (EncodeGuard(expected) != GuardRequiredBytes)
            return false;
        for (uint i = 0; i < GuardRequiredBytes; i++)
            if (code[i] != expected[i])
                return false;
        return true;
    }

    /// <summary>
    /// ulong (uint msr), Win64 ABI. Only the adapter's admitted architectural
    /// MSR indices may be used, after CPL0/CPUID checks. No host invocation.
    /// </summary>

    public static bool EmitReadMsr(byte* destination, uint capacity)
    {
        if (!Admit(destination, capacity, ReadMsrBytes))
            return false;
        uint p = 0;
        Op(destination, ref p, 0xfa1e0ff3, 4); // endbr64
        Op(destination, ref p, 0x320f, 2); // rdmsr (ECX from first argument)
        Op(destination, ref p, 0x20e2c148, 4); // shl rdx,32
        Op(destination, ref p, 0xd00948, 3); // or rax,rdx
        Op(destination, ref p, 0xc3, 1); // ret
        return p == ReadMsrBytes;
    }

    /// <summary>ulong (): CPUID.1:EDX in bits 31:0, CPUID.7.0:ECX in bits 63:32.
    /// The latter is zero when basic leaf 7 is unavailable. Preserves RBX.</summary>

    public static bool EmitReadCapabilities(byte* destination, uint capacity)
    {
        if (!Admit(destination, capacity, ReadCapabilitiesBytes))
            return false;
        uint p = 0;
        Op(destination, ref p, 0xfa1e0ff3, 4); // endbr64
        Op(destination, ref p, 0x53, 1); // push rbx
        Op(destination, ref p, 0xc031, 2); // xor eax,eax
        Op(destination, ref p, 0xc931, 2); // xor ecx,ecx
        Op(destination, ref p, 0xa20f, 2); // cpuid 0
        Op(destination, ref p, 0xc08941, 3); // mov r8d,eax
        Op(destination, ref p, 0x00000001b8, 5); // mov eax,1
        Op(destination, ref p, 0xa20f, 2); // cpuid 1
        Op(destination, ref p, 0xd18941, 3); // mov r9d,edx
        Op(destination, ref p, 0xd231, 2); // xor edx,edx
        Op(destination, ref p, 0x07f88341, 4); // cmp r8d,7
        Op(destination, ref p, 0x0b72, 2); // jb combine
        Op(destination, ref p, 0x00000007b8, 5); // mov eax,7
        Op(destination, ref p, 0xc931, 2); // xor ecx,ecx
        Op(destination, ref p, 0xa20f, 2); // cpuid 7.0
        Op(destination, ref p, 0xca89, 2); // mov edx,ecx
        Op(destination, ref p, 0xc88944, 3); // combine: mov eax,r9d
        Op(destination, ref p, 0x20e2c148, 4); // shl rdx,32
        Op(destination, ref p, 0xd00948, 3); // or rax,rdx
        Op(destination, ref p, 0xc35b, 2); // pop rbx; ret
        return p == ReadCapabilitiesBytes;
    }

    /// <summary>
    /// uint (ulong expectedPat, ulong replacementPat, ulong ownedCr3), Win64 ABI.
    /// The adapter must prove PAT slot 4 unused by every reachable leaf. Only a
    /// WB/WC exchange in that slot is admitted. Other slots remain unchanged.
    /// The stub checks the current MSR, writes PAT, invalidates translations and
    /// checks readback. A readback failure after WRMSR requires terminal failure.
    /// </summary>

    public static bool EmitWritePat(byte* destination, uint capacity)
    {
        uint bytes = WritePatBytes;
        if (!Admit(destination, capacity, bytes))
            return false;
        return EncodeWritePat(destination, bytes - 3) == bytes;
    }

    public static bool ValidateWritePatReadBack(byte* code, uint capacity)
    {
        uint bytes = WritePatBytes;
        if (bytes > 512 || !Admit(code, capacity, bytes))
            return false;
        byte* expected = stackalloc byte[512];
        if (EncodeWritePat(expected, bytes - 3) != bytes)
            return false;
        for (uint i = 0; i < bytes; i++)
            if (code[i] != expected[i])
                return false;
        return true;
    }

    private static uint EncodeWritePat(byte* code, uint failure)
    {
        uint p = 0;
        ControlGuards(code, ref p, failure);
        Op(code, ref p, 0xd8200f, 3); // mov rax,cr3
        Op(code, ref p, 0xc0394c, 3); // cmp rax,r8
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xc98949, 3); // mov r9,rcx (expected PAT)
        Op(code, ref p, 0xd28949, 3); // mov r10,rdx (replacement PAT)
        Op(code, ref p, 0xc38949, 3); // mov r11,rax (owned CR3)
        Op(code, ref p, 0xc8894c, 3); // mov rax,r9
        Op(code, ref p, 0xd0314c, 3); // xor rax,r10
        Op(code, ref p, 0xba48, 2); // mov rdx,all bits except PAT4
        Op(code, ref p, 0xffffff00ffffffff, 8);
        Op(code, ref p, 0xd08548, 3); // test rax,rdx
        Branch(code, ref p, 0x85, failure);
        PatTypeGuard(code, ref p, failure, 0xc8894c); // mov rax,r9
        PatTypeGuard(code, ref p, failure, 0xd0894c); // mov rax,r10
        Op(code, ref p, 0xd1394d, 3); // cmp r9,r10
        Branch(code, ref p, 0x84, failure); // unchanged is not an update
        Op(code, ref p, 0x00000277b9, 5); // mov ecx,IA32_PAT
        Op(code, ref p, 0x320f, 2); // rdmsr
        Op(code, ref p, 0x20e2c148, 4); // shl rdx,32
        Op(code, ref p, 0xd00948, 3); // or rax,rdx
        Op(code, ref p, 0xc8394c, 3); // cmp rax,r9 (reject stale PAT)
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xd0894c, 3); // mov rax,r10
        Op(code, ref p, 0xd2894c, 3); // mov rdx,r10
        Op(code, ref p, 0x20eac148, 4); // shr rdx,32
        Op(code, ref p, 0x300f, 2); // wrmsr
        Op(code, ref p, 0xdb220f41, 4); // mov cr3,r11
        Op(code, ref p, 0x320f, 2); // rdmsr (same IA32_PAT)
        Op(code, ref p, 0x20e2c148, 4); // shl rdx,32
        Op(code, ref p, 0xd00948, 3); // or rax,rdx
        Op(code, ref p, 0xd0394c, 3); // cmp rax,r10 (actual readback)
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0x00000001b8, 5); // mov eax,1
        Op(code, ref p, 0xc3, 1);
        Op(code, ref p, 0xc3c031, 3); // fail: xor eax,eax; ret
        return p;
    }

    private static void PatTypeGuard(byte* code, ref uint p, uint failure, ulong load)
    {
        Op(code, ref p, load, 3);
        Op(code, ref p, 0x20e8c148, 4); // shr rax,32
        Op(code, ref p, 0x000000ff25, 5); // and eax,255
        Op(code, ref p, 0x06f883, 3); // cmp eax,6
        Branch(code, ref p, 0x87, failure); // ja fail
        Op(code, ref p, 0x00000042ba, 5); // mov edx,(1<<WB)|(1<<WC)
        Op(code, ref p, 0xc2a30f, 3); // bt edx,eax
        Branch(code, ref p, 0x83, failure); // jnc fail
    }

    private static uint Encode(byte* code)
    {
        const uint failure = RequiredBytes - 3;
        uint p = 0;
        ControlGuards(code, ref p, failure);
        Op(code, ref p, 0xd8200f, 3); // mov rax,cr3
        Op(code, ref p, 0xc8394c, 3); // cmp rax,r9 (original owned root)
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xc98548, 3); // test rcx,rcx
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0x00000007c1f748, 7); // test rcx,7
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0x01c2f6, 3); // test dl,1 (old entry must be present)
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0x01c0f641, 4); // test r8b,1 (new entry must be present)
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0x113948, 3); // cmp [rcx],rdx (reject stale entry)
        Branch(code, ref p, 0x85, failure);
        // No failure return after the first write. The admitted code, stack and
        // paging structures remain mapped while the target leaf is absent.
        Op(code, ref p, 0xf0ae0f, 3); // mfence: drain prior stores
        Op(code, ref p, 0xd28949, 3); // mov r10,rdx
        Op(code, ref p, 0xfee28349, 4); // and r10,-2
        Op(code, ref p, 0x11894c, 3); // mov [rcx],r10: clear present
        Op(code, ref p, 0xd8220f, 3); // mov cr3,rax: invalidate TLB/walk caches
        Op(code, ref p, 0x01894c, 3); // mov [rcx],r8: install replacement
        Op(code, ref p, 0x090f, 2); // wbinvd: complete cache maintenance
        Op(code, ref p, 0x00000001b8, 5); // mov eax,1
        Op(code, ref p, 0xc3, 1); // ret
        Op(code, ref p, 0xc3c031, 3); // fail: xor eax,eax; ret
        return p;
    }

    private static uint EncodeGuard(byte* code)
    {
        const uint failure = GuardRequiredBytes - 3;
        uint p = 0;
        ControlGuards(code, ref p, failure);
        Op(code, ref p, 0xd8200f, 3); // mov rax,cr3
        Op(code, ref p, 0xc8394c, 3); // cmp rax,r9 (owned root)
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xc98548, 3); // test rcx,rcx
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0x00000007c1f748, 7); // test rcx,7
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xd28548, 3); // test rdx,rdx
        Op(code, ref p, 0x0974, 2); // zero skips present-only check
        Op(code, ref p, 0x01c2f6, 3); // nonzero old word must be present
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0xc0854d, 3); // test r8,r8
        Op(code, ref p, 0x0a74, 2); // zero skips present-only check
        Op(code, ref p, 0x01c0f641, 4); // nonzero new word must be present
        Branch(code, ref p, 0x84, failure);
        Op(code, ref p, 0x113948, 3); // cmp [rcx],rdx (exact expected word)
        Branch(code, ref p, 0x85, failure);
        // No failure return follows a write. Revoke, invalidate and install
        // while the independently owned code/stack/table pages stay mapped.
        Op(code, ref p, 0xf0ae0f, 3); // mfence
        Op(code, ref p, 0xd28949, 3); // mov r10,rdx
        Op(code, ref p, 0xfee28349, 4); // and r10,-2
        Op(code, ref p, 0x11894c, 3); // clear present
        Op(code, ref p, 0xd8220f, 3); // reload current root
        Op(code, ref p, 0x01894c, 3); // install replacement, including zero
        Op(code, ref p, 0x090f, 2); // wbinvd
        Op(code, ref p, 0x00000001b8, 5);
        Op(code, ref p, 0xc3, 1);
        Op(code, ref p, 0xc3c031, 3);
        return p;
    }

    private static void ControlGuards(byte* code, ref uint p, uint failure)
    {
        Op(code, ref p, 0xfa1e0ff3, 4); // endbr64
        Op(code, ref p, 0xc88c66, 3); // mov ax,cs
        Op(code, ref p, 0x03a8, 2); // test al,3
        Branch(code, ref p, 0x85, failure); // jne fail: no privileged host path
        Op(code, ref p, 0x589c, 2); // pushfq; pop rax (balanced 8-byte use)
        Op(code, ref p, 0x00000700a9, 5); // test eax,TF|IF|DF
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xc0200f, 3); // mov rax,cr0
        Op(code, ref p, 0x60000000a9, 5); // test eax,CD|NW
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0x8001000125, 5); // and eax,PG|WP|PE
        Op(code, ref p, 0x800100013d, 5); // cmp eax,PG|WP|PE
        Branch(code, ref p, 0x85, failure);
        Op(code, ref p, 0xe0200f, 3); // mov rax,cr4
        Op(code, ref p, 0x00021080a9, 5); // test eax,PCIDE|LA57|PGE
        Branch(code, ref p, 0x85, failure);
    }

    private static bool Admit(byte* destination, uint capacity, uint required) => destination != null && capacity >= required && (ulong)destination >= 4096 && (ulong)destination < LowEnd - required;
    private static void Branch(byte* code, ref uint p, byte condition, uint target)
    {
        Op(code, ref p, (ulong)(0x0F | (condition << 8)), 2);
        Op(code, ref p, code == null ? 0 : target - (p + 4), 4);
    }

    private static void Op(byte* code, ref uint p, ulong value, uint bytes)
    {
        for (uint i = 0; i < bytes; i++)
        {
            if (code != null)
                code[p] = (byte)(value >> (int)(i * 8));
            p++;
        }
    }
}
