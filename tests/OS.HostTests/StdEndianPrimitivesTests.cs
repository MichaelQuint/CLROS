using System.Runtime.CompilerServices;
using OS.Std.Binary;

internal static class StdEndianPrimitivesTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        const string name = "std.endian primitives";
        HostCheck.Run(name, () =>
        {
            TestReadExactByteOrder();
            TestPointerUInt16();
            TestWriteExactByteOrder();
            TestTryReadShortBuffers();
            TestTryWriteShortBuffers();
            TestRoundTrips();
        });
    }

    private static unsafe void TestPointerUInt16()
    {
        byte* bytes = stackalloc byte[33];
        bytes[0] = 0xa5;
        bytes[1] = 0x34;
        bytes[2] = 0x12;
        HostCheck.Equal((ushort)0x1234, EndianPrimitives.ReadUInt16LittleEndian(bytes + 1));
        bytes[31] = 0xcd;
        bytes[32] = 0xab;
        HostCheck.Equal((ushort)0xabcd, EndianPrimitives.ReadUInt16LittleEndian(bytes + 31));
        bytes[31] = bytes[32] = 0;
        HostCheck.Equal((ushort)0, EndianPrimitives.ReadUInt16LittleEndian(bytes + 31));
        bytes[31] = bytes[32] = 0xff;
        HostCheck.Equal(ushort.MaxValue, EndianPrimitives.ReadUInt16LittleEndian(bytes + 31));
        HostCheck.Equal((byte)0xa5, bytes[0]);
    }

    private static void TestReadExactByteOrder()
    {
        ExpectValue("ReadUInt16LittleEndian", 0x1234, EndianPrimitives.ReadUInt16LittleEndian(new byte[] { 0x34, 0x12 }));
        ExpectValue("ReadUInt16BigEndian", 0x1234, EndianPrimitives.ReadUInt16BigEndian(new byte[] { 0x12, 0x34 }));

        ExpectValue("ReadUInt32LittleEndian", 0x12345678, EndianPrimitives.ReadUInt32LittleEndian(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
        ExpectValue("ReadUInt32BigEndian", 0x12345678, EndianPrimitives.ReadUInt32BigEndian(new byte[] { 0x12, 0x34, 0x56, 0x78 }));

        ExpectValue("ReadUInt64LittleEndian", 0x0123456789ABCDEF, EndianPrimitives.ReadUInt64LittleEndian(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 }));
        ExpectValue("ReadUInt64BigEndian", 0x0123456789ABCDEF, EndianPrimitives.ReadUInt64BigEndian(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }));
    }

    private static void TestWriteExactByteOrder()
    {
        byte[] buffer = new byte[8];

        EndianPrimitives.WriteUInt16LittleEndian(buffer, 0x1234);
        ExpectBytes("WriteUInt16LittleEndian", buffer, 0x34, 0x12);

        Fill(buffer, 0);
        EndianPrimitives.WriteUInt16BigEndian(buffer, 0x1234);
        ExpectBytes("WriteUInt16BigEndian", buffer, 0x12, 0x34);

        Fill(buffer, 0);
        EndianPrimitives.WriteUInt32LittleEndian(buffer, 0x12345678);
        ExpectBytes("WriteUInt32LittleEndian", buffer, 0x78, 0x56, 0x34, 0x12);

        Fill(buffer, 0);
        EndianPrimitives.WriteUInt32BigEndian(buffer, 0x12345678);
        ExpectBytes("WriteUInt32BigEndian", buffer, 0x12, 0x34, 0x56, 0x78);

        Fill(buffer, 0);
        EndianPrimitives.WriteUInt64LittleEndian(buffer, 0x0123456789ABCDEF);
        ExpectBytes("WriteUInt64LittleEndian", buffer, 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01);

        Fill(buffer, 0);
        EndianPrimitives.WriteUInt64BigEndian(buffer, 0x0123456789ABCDEF);
        ExpectBytes("WriteUInt64BigEndian", buffer, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF);
    }

    private static void TestTryReadShortBuffers()
    {
        byte[] buffer = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11 };

        ExpectFalse("TryReadUInt16LittleEndian short", EndianPrimitives.TryReadUInt16LittleEndian(buffer.AsSpan(0, 1), out ushort shortUInt16));
        ExpectValue("TryReadUInt16LittleEndian zero", 0, shortUInt16);
        ExpectFalse("TryReadUInt16BigEndian short", EndianPrimitives.TryReadUInt16BigEndian(buffer.AsSpan(0, 1), out shortUInt16));
        ExpectValue("TryReadUInt16BigEndian zero", 0, shortUInt16);

        ExpectFalse("TryReadUInt32LittleEndian short", EndianPrimitives.TryReadUInt32LittleEndian(buffer.AsSpan(0, 3), out uint shortUInt32));
        ExpectValue("TryReadUInt32LittleEndian zero", 0, shortUInt32);
        ExpectFalse("TryReadUInt32BigEndian short", EndianPrimitives.TryReadUInt32BigEndian(buffer.AsSpan(0, 3), out shortUInt32));
        ExpectValue("TryReadUInt32BigEndian zero", 0, shortUInt32);

        ExpectFalse("TryReadUInt64LittleEndian short", EndianPrimitives.TryReadUInt64LittleEndian(buffer, out ulong shortUInt64));
        ExpectValue("TryReadUInt64LittleEndian zero", 0, shortUInt64);
        ExpectFalse("TryReadUInt64BigEndian short", EndianPrimitives.TryReadUInt64BigEndian(buffer, out shortUInt64));
        ExpectValue("TryReadUInt64BigEndian zero", 0, shortUInt64);
    }

    private static void TestTryWriteShortBuffers()
    {
        byte[] buffer = new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 };

        ExpectFalse("TryWriteUInt16LittleEndian short", EndianPrimitives.TryWriteUInt16LittleEndian(buffer.AsSpan(0, 1), 0x1234));
        ExpectAll("TryWriteUInt16LittleEndian unchanged", buffer, 0xA5);
        ExpectFalse("TryWriteUInt16BigEndian short", EndianPrimitives.TryWriteUInt16BigEndian(buffer.AsSpan(0, 1), 0x1234));
        ExpectAll("TryWriteUInt16BigEndian unchanged", buffer, 0xA5);

        ExpectFalse("TryWriteUInt32LittleEndian short", EndianPrimitives.TryWriteUInt32LittleEndian(buffer.AsSpan(0, 3), 0x12345678));
        ExpectAll("TryWriteUInt32LittleEndian unchanged", buffer, 0xA5);
        ExpectFalse("TryWriteUInt32BigEndian short", EndianPrimitives.TryWriteUInt32BigEndian(buffer.AsSpan(0, 3), 0x12345678));
        ExpectAll("TryWriteUInt32BigEndian unchanged", buffer, 0xA5);

        ExpectFalse("TryWriteUInt64LittleEndian short", EndianPrimitives.TryWriteUInt64LittleEndian(buffer, 0x0123456789ABCDEF));
        ExpectAll("TryWriteUInt64LittleEndian unchanged", buffer, 0xA5);
        ExpectFalse("TryWriteUInt64BigEndian short", EndianPrimitives.TryWriteUInt64BigEndian(buffer, 0x0123456789ABCDEF));
        ExpectAll("TryWriteUInt64BigEndian unchanged", buffer, 0xA5);
    }

    private static void TestRoundTrips()
    {
        byte[] buffer = new byte[8];

        ExpectTrue("TryWriteUInt16LittleEndian", EndianPrimitives.TryWriteUInt16LittleEndian(buffer, 0xC0DE));
        ExpectTrue("TryReadUInt16LittleEndian", EndianPrimitives.TryReadUInt16LittleEndian(buffer, out ushort roundUInt16));
        ExpectValue("UInt16 LE round trip", 0xC0DE, roundUInt16);

        ExpectTrue("TryWriteUInt16BigEndian", EndianPrimitives.TryWriteUInt16BigEndian(buffer, 0xBEEF));
        ExpectTrue("TryReadUInt16BigEndian", EndianPrimitives.TryReadUInt16BigEndian(buffer, out roundUInt16));
        ExpectValue("UInt16 BE round trip", 0xBEEF, roundUInt16);

        ExpectTrue("TryWriteUInt32LittleEndian", EndianPrimitives.TryWriteUInt32LittleEndian(buffer, 0x89ABCDEF));
        ExpectTrue("TryReadUInt32LittleEndian", EndianPrimitives.TryReadUInt32LittleEndian(buffer, out uint roundUInt32));
        ExpectValue("UInt32 LE round trip", 0x89ABCDEF, roundUInt32);

        ExpectTrue("TryWriteUInt32BigEndian", EndianPrimitives.TryWriteUInt32BigEndian(buffer, 0x76543210));
        ExpectTrue("TryReadUInt32BigEndian", EndianPrimitives.TryReadUInt32BigEndian(buffer, out roundUInt32));
        ExpectValue("UInt32 BE round trip", 0x76543210, roundUInt32);

        ExpectTrue("TryWriteUInt64LittleEndian", EndianPrimitives.TryWriteUInt64LittleEndian(buffer, 0x0123456789ABCDEF));
        ExpectTrue("TryReadUInt64LittleEndian", EndianPrimitives.TryReadUInt64LittleEndian(buffer, out ulong roundUInt64));
        ExpectValue("UInt64 LE round trip", 0x0123456789ABCDEF, roundUInt64);

        ExpectTrue("TryWriteUInt64BigEndian", EndianPrimitives.TryWriteUInt64BigEndian(buffer, 0xFEDCBA9876543210));
        ExpectTrue("TryReadUInt64BigEndian", EndianPrimitives.TryReadUInt64BigEndian(buffer, out roundUInt64));
        ExpectValue("UInt64 BE round trip", 0xFEDCBA9876543210, roundUInt64);
    }

    private static void ExpectTrue(string scenario, bool actual)
    {
        if (!actual)
        {
            throw new InvalidOperationException(scenario + " expected true.");
        }
    }

    private static void ExpectFalse(string scenario, bool actual)
    {
        if (actual)
        {
            throw new InvalidOperationException(scenario + " expected false.");
        }
    }

    private static void ExpectValue(string scenario, ulong expected, ulong actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected 0x" + expected.ToString("X") + " but returned 0x" + actual.ToString("X") + ".");
        }
    }

    private static void ExpectBytes(string scenario, byte[] buffer, params byte[] expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (buffer[i] != expected[i])
            {
                throw new InvalidOperationException(scenario + " byte " + i + " expected 0x" + expected[i].ToString("X2") + " but returned 0x" + buffer[i].ToString("X2") + ".");
            }
        }
    }

    private static void ExpectAll(string scenario, byte[] buffer, byte expected)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != expected)
            {
                throw new InvalidOperationException(scenario + " byte " + i + " expected 0x" + expected.ToString("X2") + " but returned 0x" + buffer[i].ToString("X2") + ".");
            }
        }
    }

    private static void Fill(byte[] buffer, byte value)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = value;
        }
    }
}
