using System.Buffers.Binary;
using System.Text;
using KaguyaArcTool.Compression;

namespace KaguyaArcTool.Image;

internal static class Aps3Image
{
    public static bool IsAps3(ReadOnlySpan<byte> data)
    {
        return data.Length >= 9
            && data[0] == 0x04
            && data[1] == (byte)'A'
            && data[2] == (byte)'P'
            && data[3] == (byte)'S'
            && data[4] == (byte)'3';
    }

    public static Aps3ReadResult Decode(byte[] data, string originalName)
    {
        ParsedAps parsed = Parse(data, originalName);
        byte[] apData;
        if (parsed.Metadata.Compression == 1)
        {
            byte[] packed = data.AsSpan(parsed.DataOffset, parsed.Metadata.PackedSize).ToArray();
            apData = KaguyaLz.Unpack(packed, parsed.Metadata.UnpackedSize);
        }
        else
        {
            apData = data.AsSpan(parsed.DataOffset, parsed.Metadata.UnpackedSize).ToArray();
        }

        ImageMetadata apMetadata = ApImage.ReadMetadata(apData, originalName);
        parsed.Metadata.Width = apMetadata.Width;
        parsed.Metadata.Height = apMetadata.Height;
        parsed.Metadata.Bpp = apMetadata.Bpp;
        parsed.Metadata.UnpackedSize = apData.Length;
        return new Aps3ReadResult(parsed.Metadata, apData);
    }

    public static ImageMetadata ReadMetadata(byte[] data, string originalName)
    {
        ParsedAps parsed = Parse(data, originalName);
        if (parsed.Metadata.Compression == 1)
        {
            byte[] packed = data.AsSpan(parsed.DataOffset, parsed.Metadata.PackedSize).ToArray();
            byte[] apData = KaguyaLz.Unpack(packed, parsed.Metadata.UnpackedSize);
            ImageMetadata apMetadata = ApImage.ReadMetadata(apData, originalName);
            parsed.Metadata.Width = apMetadata.Width;
            parsed.Metadata.Height = apMetadata.Height;
            parsed.Metadata.Bpp = apMetadata.Bpp;
            parsed.Metadata.UnpackedSize = apData.Length;
        }

        return parsed.Metadata;
    }

    public static byte[] Encode(byte[] apData, ImageMetadata metadata, string outputName)
    {
        byte[] header = metadata.ApsHeaderBase64 is { Length: > 0 }
            ? Convert.FromBase64String(metadata.ApsHeaderBase64)
            : CreateDefaultHeader(outputName, metadata.Width, metadata.Height);

        using MemoryStream output = new();
        output.Write(header);

        byte[] packed = KaguyaLz.Pack(apData);
        int dataSize = checked(2 + 4 + 4 + packed.Length);
        WriteInt32(output, dataSize);
        WriteUInt16(output, 1);
        WriteInt32(output, packed.Length);
        WriteInt32(output, apData.Length);
        output.Write(packed);
        return output.ToArray();
    }

    private static ParsedAps Parse(byte[] data, string originalName)
    {
        if (!IsAps3(data))
            throw new InvalidDataException("APS3 image must start with \\x04APS3");

        int position = 5;
        int count = ReadInt32(data, ref position);
        if (count <= 0 || count > 10000)
            throw new InvalidDataException("invalid APS3 tile count");

        for (int i = 0; i < count; i++)
        {
            Require(data, position, 5);
            position += 4;
            int nameLength = data[position++];
            Require(data, position, nameLength + 28);
            position += nameLength + 28;
        }

        int headerLength = position;
        int dataSize = ReadInt32(data, ref position);
        Require(data, position, dataSize);
        int compression = ReadUInt16(data, ref position);
        int packedSize = 0;
        int unpackedSize;
        if (compression == 0)
        {
            unpackedSize = ReadInt32(data, ref position);
            packedSize = unpackedSize;
        }
        else if (compression == 1)
        {
            packedSize = ReadInt32(data, ref position);
            unpackedSize = ReadInt32(data, ref position);
        }
        else
        {
            throw new InvalidDataException("unsupported APS3 compression");
        }

        Require(data, position, packedSize);
        ImageMetadata metadata = new()
        {
            Format = "APS3",
            OriginalName = originalName,
            Compression = compression,
            PackedSize = packedSize,
            UnpackedSize = unpackedSize,
            ApsHeaderBase64 = Convert.ToBase64String(data.AsSpan(0, headerLength).ToArray())
        };

        return new ParsedAps(metadata, position);
    }

    private static byte[] CreateDefaultHeader(string outputName, int width, int height)
    {
        Encoding encoding = Encoding.GetEncoding(932);
        byte[] name = encoding.GetBytes(Path.GetFileName(outputName));
        if (name.Length > byte.MaxValue)
            name = name.AsSpan(0, byte.MaxValue).ToArray();

        using MemoryStream output = new();
        output.WriteByte(0x04);
        output.WriteByte((byte)'A');
        output.WriteByte((byte)'P');
        output.WriteByte((byte)'S');
        output.WriteByte((byte)'3');
        WriteInt32(output, 2);
        WriteInt32(output, 0);
        output.WriteByte((byte)name.Length);
        output.Write(name);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, width);
        WriteInt32(output, height);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        output.WriteByte(0);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        WriteInt32(output, -1);
        WriteInt32(output, 0);
        WriteInt32(output, 0);
        return output.ToArray();
    }

    private static int ReadInt32(byte[] data, ref int position)
    {
        Require(data, position, 4);
        int value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    private static int ReadUInt16(byte[] data, ref int position)
    {
        Require(data, position, 2);
        int value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position, 2));
        position += 2;
        return value;
    }

    private static void WriteInt32(Stream output, int value)
    {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        output.Write(data);
    }

    private static void WriteUInt16(Stream output, ushort value)
    {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        output.Write(data);
    }

    private static void Require(byte[] data, int position, int count)
    {
        if (position < 0 || count < 0 || position + count > data.Length)
            throw new EndOfStreamException("APS3 image data ended early");
    }

    private sealed record ParsedAps(ImageMetadata Metadata, int DataOffset);
}

internal sealed record Aps3ReadResult(ImageMetadata Metadata, byte[] ApData);
