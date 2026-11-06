using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe class BoundedPCIConfigurationSpaceInterfaceTests
{
    [HostTest("m05.004 ecam address is relative to bus zero and rejects invalid bounds")]
    private static void Address()
    {
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 3, 8, 31, 8, 9, out var window));
        HostCheck.True(window.IsValid && window.Start == 0xe0800000 && window.Bytes == 0x200000);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Address(window, 9, 31, 7, 4092, out ulong address) == BoundedPciStatus.Success);
        HostCheck.True(address == 0xe09ffffc);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Address(window, 7, 0, 0, 0, out address) == BoundedPciStatus.OutsideWindow && address == 0);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Address(window, 8, 32, 0, 0, out _) == BoundedPciStatus.InvalidTuple);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Address(window, 8, 0, 8, 0, out _) == BoundedPciStatus.InvalidTuple);
        foreach (uint offset in new uint[] { 1, 2, 3, 4093, 4096, uint.MaxValue })
            HostCheck.True(BoundedPCIConfigurationSpaceInterface.Address(window, 8, 0, 0, offset, out address) == BoundedPciStatus.InvalidRegister && address == 0);
        foreach (ulong origin in new ulong[] { 0, 1, 0xe0000001, 0xfffff000, ulong.MaxValue })
            HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryWindow(origin, 0, 0, 255, 0, 0, out _));
        HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 2, 1, 2, 2, out _));
        HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 0, 4, out _));
        HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryWindow(0xf8000000, 0, 0, 255, 0, 0, out _));
        HostCheck.True(!default(BoundedPciWindow).IsValid);
    }

    [HostTest("m05.004 invalid and revoked sessions do not invoke hardware")]
    private static void Session()
    {
        uint calls = 0;
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 0, 0, out var window));
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &calls, &Read, out var access));
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Read32(ref access, 0, 2, 3, 4, out uint value) == BoundedPciStatus.Success);
        HostCheck.True(value == 0xe0013004 && calls == 1);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.Read32(ref access, 1, 0, 0, 0, out _) == BoundedPciStatus.OutsideWindow && calls == 1);
        BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
        HostCheck.True(!access.IsActive && BoundedPCIConfigurationSpaceInterface.Read32(ref access, 0, 0, 0, 0, out value) == BoundedPciStatus.InvalidAccess && value == 0 && calls == 1);
        HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryOpen(window, null, null, out _));
        HostCheck.True(!BoundedPCIConfigurationSpaceInterface.TryOpen(default, null, &Read, out _));
    }

    [HostTest("m05.004 native read has exact DWORD ABI and padding")]
    private static void NativeBytes()
    {
        byte* code = stackalloc byte[18];
        new Span<byte>(code, 18).Fill(0xa5);
        HostCheck.True(!X64PciConfigurationRead.Emit(code + 1, 15) && code[1] == 0xa5);
        HostCheck.True(X64PciConfigurationRead.Emit(code + 1, 16));
        byte[] expected = [0xf3, 0x0f, 0x1e, 0xfa, 0x8b, 0x02, 0xc3, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        HostCheck.True(new ReadOnlySpan<byte>(code + 1, 16).SequenceEqual(expected) && code[0] == 0xa5 && code[17] == 0xa5);
        for (int i = 0; i < 16; i++)
        {
            code[1 + i] ^= 1;
            HostCheck.True(!X64PciConfigurationRead.Validate(code + 1, 16));
            code[1 + i] ^= 1;
        }
    }

    [UnmanagedCallersOnly]
    private static uint Read(void* context, ulong address)
    {
        (*(uint*)context)++;
        return (uint)address;
    }
}
