using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;
using Leaf = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification;

internal static unsafe partial class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* _code = (byte*)NativeMemory.AlignedAlloc(16384, 4096);
        private readonly byte* _data = (byte*)NativeMemory.AlignedAlloc(36864, 4096);
        public byte* Code => _code + 4096;
        public byte* Data => _data + 4096;
        public byte* Context => Data + 4096;
        public byte* Capture => Data + 28488;

        public UnexpectedVectorPanicRequest R;
        public Fixture(UnexpectedVectorPanicRequest? request = null)
        {
            new Span<byte>(_code, 16384).Fill(0xa5);
            new Span<byte>(_data, 36864).Fill(0xa5);
            R = request ?? Request();
            Prepare();
        }

        public void Prepare()
        {
            Require(Leaf.PrepareContext(Code, 8192, Data, 28672, R).Succeeded, "prepare");
            Require(Leaf.EmitStream(Code, 8192, Data, 28672, R).Succeeded, "emit");
            Require(Leaf.ValidateStreamReadBack(Code, 8192, Data, 28672, R).Succeeded, "pristine readback");
        }

        public void Captured(ulong salt = 0)
        {
            Prepare();
            for (int i = 0; i < 15; i++)
                Q(Capture, i * 8, 0xfedcba9876500000UL + (ulong)i + salt);
            Q(Capture, 120, 32);
            Q(Capture, 128, 0);
            Q(Capture, 136, 0);
            Q(Capture, 144, R.CodeAddress + 4096 + 960 + 2);
            Q(Capture, 152, R.FirmwareCs);
            Q(Capture, 160, 0x446);
            Q(Capture, 168, R.DataAddress + 28672);
            Q(Capture, 176, 16);
            Context[10] = 39;
            Q(Context, 12, R.DataAddress + 4096 + 24);
            Context[20] = 24;
            Context[37] |= 1;
            Context[45] |= 1;
            Context[53] = 0x8b;
            Context[192] = 0xff;
            Context[193] = 15;
            Q(Context, 194, R.DataAddress);
            Q(Context, 288, R.DataAddress + 28488);
            Q(Context, 296, 0xfeedface12345678 + salt);
            Q(Context, 304, R.FirmwareCr3);
            Q(Context, 312, 8);
            Q(Context, 320, 16);
            Q(Context, 328, 16);
            Q(Context, 336, 16);
            Q(Context, 344, 24);
            Q(Context, 352, R.FirmwareCs);
            Q(Context, 360, 0x00a09b00);
            Q(Context, 368, R.FirmwareCr3);
            Require(Leaf.ValidateCapturedState(Code, 8192, Data, 28672, R, out _).Succeeded, "synthetic state");
        }

        public void Canaries()
        {
            for (int i = 0; i < 4096; i++)
                Require(_code[i] == 0xa5 && _code[12288 + i] == 0xa5 && _data[i] == 0xa5 && _data[32768 + i] == 0xa5, "full-span canary");
        }

        public void Dispose()
        {
            Canaries();
            NativeMemory.AlignedFree(_code);
            NativeMemory.AlignedFree(_data);
        }
    }
}
