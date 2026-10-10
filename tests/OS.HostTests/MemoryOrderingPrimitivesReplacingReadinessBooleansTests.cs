using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Std.Memory;

internal static unsafe class MemoryOrderingPrimitivesReplacingReadinessBooleansTests
{
    private struct CallbackState { public uint Trace, Calls; public byte Result; }

    [HostTest("virtqueue.fence invokes the actual callback between caller operations", HostLane.Fast, 0)]
    private static void CallbackOrder()
    {
        HostCheck.True(default(MemoryOrderStatus) != MemoryOrderStatus.Success);
        CallbackState state = new() { Trace = 1, Result = 1 };
        HostCheck.Equal(MemoryOrderStatus.Success, MemoryOrder.Fence(&state, &Barrier));
        HostCheck.Equal(1U, state.Calls);
        HostCheck.Equal(0x12U, state.Trace);
        state.Trace = state.Trace * 16 + 3;
        HostCheck.Equal(0x123U, state.Trace);
        HostCheck.Equal(MemoryOrderStatus.Success, MemoryOrder.Fence(null, &NullContext));
    }

    [HostTest("virtqueue.fence rejects null callbacks and preserves effects of failed callbacks", HostLane.Fast, 1)]
    private static void CallbackFailures()
    {
        CallbackState state = new() { Trace = 1, Result = 1 };
        HostCheck.Equal(MemoryOrderStatus.InvalidCallback, MemoryOrder.Fence(&state, null));
        HostCheck.Equal(0U, state.Calls); HostCheck.Equal(1U, state.Trace);
        foreach (byte result in new byte[] { 0, 2, 255 })
        {
            state = new() { Trace = 1, Result = result };
            HostCheck.Equal(MemoryOrderStatus.Failed, MemoryOrder.Fence(&state, &Barrier));
            HostCheck.Equal(1U, state.Calls); HostCheck.Equal(0x12U, state.Trace);
        }
    }

    [HostTest("virtqueue.fence emits exact independent x64 bytes and rejects every mutation", HostLane.Fast, 2)]
    private static void InstructionBytes()
    {
        byte* storage = stackalloc byte[18];
        new Span<byte>(storage, 18).Fill(0xa5);
        byte[] expected = [0xf3, 0x0f, 0x1e, 0xfa, 0x0f, 0xae, 0xf0, 0xb8, 0x01, 0, 0, 0, 0xc3, 0, 0, 0];
        HostCheck.Equal(16U, X64VirtqueueMemoryFence.Bytes);
        HostCheck.True(X64VirtqueueMemoryFence.Emit(storage + 1, 16));
        HostCheck.True(new ReadOnlySpan<byte>(storage + 1, 16).SequenceEqual(expected));
        HostCheck.True(storage[0] == 0xa5 && storage[17] == 0xa5);
        for (int i = 0; i < 16; i++)
        {
            storage[i + 1] ^= 1;
            HostCheck.True(!X64VirtqueueMemoryFence.Validate(storage + 1, 16));
            storage[i + 1] ^= 1;
        }
        HostCheck.True(X64VirtqueueMemoryFence.Validate(storage + 1, 16));
    }

    [HostTest("virtqueue.fence rejects invalid code storage without mutation", HostLane.Fast, 3)]
    private static void CodeStorage()
    {
        byte* storage = stackalloc byte[18];
        new Span<byte>(storage, 18).Fill(0xa5);
        foreach (uint capacity in new uint[] { 0, 15 })
        {
            HostCheck.True(!X64VirtqueueMemoryFence.Emit(storage + 1, capacity));
            HostCheck.True(!X64VirtqueueMemoryFence.Validate(storage + 1, capacity));
            HostCheck.True(new ReadOnlySpan<byte>(storage, 18).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        HostCheck.True(!X64VirtqueueMemoryFence.Emit(null, 16) && !X64VirtqueueMemoryFence.Validate(null, 16));
        byte* overflowing = (byte*)(ulong.MaxValue - 7);
        HostCheck.True(!X64VirtqueueMemoryFence.Emit(overflowing, 16) && !X64VirtqueueMemoryFence.Validate(overflowing, 16));
        HostCheck.True(X64VirtqueueMemoryFence.Emit(storage + 1, 17));
        HostCheck.True(storage[0] == 0xa5 && storage[17] == 0xa5);
    }

    [UnmanagedCallersOnly]
    private static byte Barrier(void* context)
    {
        CallbackState* state = (CallbackState*)context;
        state->Trace = state->Trace * 16 + 2; state->Calls++;
        return state->Result;
    }

    [UnmanagedCallersOnly]
    private static byte NullContext(void* context) => context == null ? (byte)1 : (byte)0;
}
