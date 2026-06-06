using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace KaguyaArcTool.Image;

internal static class ApImage
{
    public static bool IsAp(ReadOnlySpan<byte> data)
    {
        return data.Length >= 12 && data[0] == (byte)'A' && data[1] == (byte)'P';
    }

    public static ImageMetadata ReadMetadata(ReadOnlySpan<byte> data, string originalName)
    {
        if (!IsAp(data))
            throw new InvalidDataException("Invalid AP image!");

        int width = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data[2..6]));
        int height = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data[6..10]));
        int bpp = BinaryPrimitives.ReadUInt16LittleEndian(data[10..12]);
        if (width <= 0 || height <= 0 || width > 0x8000 || height > 0x8000)
            throw new InvalidDataException("invalid AP dimensions");

        if (bpp is not (8 or 24 or 32))
            throw new InvalidDataException("unsupported AP bpp");

        return new ImageMetadata
        {
            Format = "AP",
            OriginalName = originalName,
            Width = width,
            Height = height,
            Bpp = bpp,
            UnpackedSize = data.Length
        };
    }

    public static Image<Rgba32> Decode(byte[] data, ImageMetadata metadata)
    {
        int bytesPerPixel = metadata.Bpp == 8 ? 1 : 4;
        int stride = checked(metadata.Width * bytesPerPixel);
        int expectedSize = checked(12 + stride * metadata.Height);
        if (data.Length < expectedSize)
            throw new EndOfStreamException("AP image data ended early");

        Image<Rgba32> image = new(metadata.Width, metadata.Height);
        image.ProcessPixelRows(accessor =>
        {
            int source = 12;
            for (int y = metadata.Height - 1; y >= 0; y--)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                if (metadata.Bpp == 8)
                {
                    for (int x = 0; x < metadata.Width; x++)
                    {
                        byte value = data[source++];
                        row[x] = new Rgba32(value, value, value, 255);
                    }
                }
                else
                {
                    for (int x = 0; x < metadata.Width; x++)
                    {
                        byte b = data[source++];
                        byte g = data[source++];
                        byte r = data[source++];
                        byte a = data[source++];
                        row[x] = new Rgba32(r, g, b, a);
                    }
                }
            }
        });

        return image;
    }

    public static byte[] Encode(Image<Rgba32> image, int bpp)
    {
        if (bpp is not (8 or 24 or 32))
            bpp = 24;

        int bytesPerPixel = bpp == 8 ? 1 : 4;
        int stride = checked(image.Width * bytesPerPixel);
        byte[] output = new byte[checked(12 + stride * image.Height)];
        output[0] = (byte)'A';
        output[1] = (byte)'P';
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(2, 4), checked((uint)image.Width));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(6, 4), checked((uint)image.Height));
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(10, 2), checked((ushort)bpp));

        image.ProcessPixelRows(accessor =>
        {
            int destination = 12;
            for (int y = image.Height - 1; y >= 0; y--)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = row[x];
                    if (bpp == 8)
                    {
                        output[destination++] = (byte)((pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000);
                    }
                    else
                    {
                        output[destination++] = pixel.B;
                        output[destination++] = pixel.G;
                        output[destination++] = pixel.R;
                        output[destination++] = pixel.A;
                    }
                }
            }
        });

        return output;
    }
}
