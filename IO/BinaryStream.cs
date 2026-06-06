using System.Buffers.Binary;

namespace KaguyaArcTool.IO;

internal static class BinaryStream
{
    public static int ReadInt32(Stream input)
    {
        Span<byte> data = stackalloc byte[4];
        input.ReadExactly(data);
        return BinaryPrimitives.ReadInt32LittleEndian(data);
    }

    public static ushort ReadUInt16(Stream input)
    {
        Span<byte> data = stackalloc byte[2];
        input.ReadExactly(data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    public static uint ReadUInt32(Stream input)
    {
        Span<byte> data = stackalloc byte[4];
        input.ReadExactly(data);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    public static uint ReadUInt32At(FileStream file, long offset)
    {
        long restore = file.Position;
        file.Position = offset;
        uint value = ReadUInt32(file);
        file.Position = restore;
        return value;
    }

    public static void WriteUInt16(Stream output, ushort value)
    {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        output.Write(data);
    }

    public static void WriteUInt32(Stream output, uint value)
    {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        output.Write(data);
    }

    public static void WriteUInt32At(FileStream file, long offset, uint value)
    {
        long restore = file.Position;
        file.Position = offset;
        WriteUInt32(file, value);
        file.Position = restore;
    }

    public static byte[] ReadExactBytes(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        stream.ReadExactly(buffer);
        return buffer;
    }

    public static void CopyExactly(Stream input, Stream output, uint bytesToCopy, byte[] buffer)
    {
        ulong remaining = bytesToCopy;
        while (remaining > 0)
        {
            int chunk = (int)Math.Min((ulong)buffer.Length, remaining);
            input.ReadExactly(buffer.AsSpan(0, chunk));
            output.Write(buffer.AsSpan(0, chunk));
            remaining -= (uint)chunk;
        }
    }
}
