using System.Text;

namespace KaguyaArcTool.Arc;

internal static class ArcNameCodec
{
    private const int MaxFileNameLength = 0x100;
    private static readonly Encoding Cp932 = Encoding.GetEncoding(932);

    public static byte[] ReadName(Stream input, int nameLength)
    {
        if (nameLength <= 0 || nameLength > MaxFileNameLength)
            throw new InvalidDataException($"invalid entry name length: {nameLength}");

        byte[] name = new byte[nameLength];
        input.ReadExactly(name);
        return name;
    }

    public static string DecodeName(byte[] encryptedName)
    {
        byte[] name = new byte[encryptedName.Length];
        for (int i = 0; i < name.Length; i++)
            name[i] = (byte)(encryptedName[i] ^ 0xFF);

        return Cp932.GetString(name).TrimStart('\\', '/');
    }

    public static byte[] EncodeName(string name)
    {
        string archiveName = "\\" + name.Replace('/', '\\').TrimStart('/', '\\');
        byte[] bytes = Cp932.GetBytes(archiveName);
        if (bytes.Length <= 0 || bytes.Length > MaxFileNameLength)
            throw new InvalidOperationException($"encoded name is too long for ARC entry: {name}");

        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= 0xFF;

        return bytes;
    }
}
