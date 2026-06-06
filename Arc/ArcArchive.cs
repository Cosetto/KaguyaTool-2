using System.Text;
using KaguyaArcTool.Compression;
using KaguyaArcTool.IO;

namespace KaguyaArcTool.Arc;

internal sealed class ArcArchive
{
    public ArcArchive(string path, ArcKind kind, List<ArcEntry> entries)
    {
        Path = path;
        Kind = kind;
        Entries = entries;
    }

    public string Path { get; }
    public ArcKind Kind { get; }
    public List<ArcEntry> Entries { get; }

    public static ArcArchive Read(FileStream file, string path)
    {
        Span<byte> signature = stackalloc byte[4];
        file.ReadExactly(signature);

        if (signature.SequenceEqual("UF01"u8))
            return ReadUf(file, path);
        if (signature.SequenceEqual("AF01"u8))
            return ReadAf(file, path);
        if (signature.SequenceEqual("WFL1"u8))
            return ReadWfl(file, path);

        string text = Encoding.ASCII.GetString(signature);
        throw new InvalidDataException("Unsupported Archive");
    }

    public static PackSummary Write(FileStream output, IReadOnlyList<InputFile> files, ArcKind kind, string? archivePath = null)
    {
        return kind switch
        {
            ArcKind.Uf01 => WriteUf(output, files),
            ArcKind.Af01 => WriteAf(output, files),
            ArcKind.Wfl1 => WriteWfl(output, files, archivePath ?? output.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static ArcArchive ReadUf(FileStream file, string path)
    {
        uint indexOffset = BinaryStream.ReadUInt32At(file, 4);
        long indexStart = checked((long)indexOffset + 4);
        if (indexStart > file.Length)
            throw new InvalidDataException("UF01 index offset is outside the archive");

        List<ArcEntry> entries = new();
        long dataOffset = 8;
        file.Position = indexStart;

        while (file.Position < file.Length)
        {
            int nameLength = BinaryStream.ReadInt32(file);
            byte[] encryptedName = ArcNameCodec.ReadName(file, nameLength);
            string name = ArcNameCodec.DecodeName(encryptedName);
            ushort flags = BinaryStream.ReadUInt16(file);
            uint storedSize = BinaryStream.ReadUInt32(file);

            long headerEnd = checked(dataOffset + 4 + nameLength + 6);
            long payloadOffset = headerEnd;
            uint unpackedSize = storedSize;
            if ((flags & 1) != 0)
            {
                EnsurePlacement(file.Length, headerEnd, 4);
                unpackedSize = BinaryStream.ReadUInt32At(file, headerEnd);
                payloadOffset = checked(headerEnd + 4);
            }

            EnsurePlacement(file.Length, payloadOffset, storedSize);
            entries.Add(new ArcEntry(name, payloadOffset, storedSize, unpackedSize, flags));

            dataOffset = checked(payloadOffset + storedSize);
        }

        return new ArcArchive(path, ArcKind.Uf01, entries);
    }

    private static ArcArchive ReadAf(FileStream file, string path)
    {
        uint indexOffset = BinaryStream.ReadUInt32At(file, 8);
        long indexStart = checked((long)indexOffset + 8);
        if (indexStart > file.Length)
            throw new InvalidDataException("AF01 index offset is outside the archive");

        List<ArcEntry> entries = new();
        long dataOffset = 12;
        file.Position = indexStart;

        while (file.Position < file.Length)
        {
            int nameLength = BinaryStream.ReadInt32(file);
            byte[] encryptedName = ArcNameCodec.ReadName(file, nameLength);
            string name = ArcNameCodec.DecodeName(encryptedName);
            ushort flags = BinaryStream.ReadUInt16(file);

            long payloadOffset = checked(dataOffset + 4 + nameLength + 6);
            if ((flags & 1) != 0)
                payloadOffset = checked(payloadOffset + 4);

            uint packedSize = BinaryStream.ReadUInt32(file);
            uint unpackedSize = BinaryStream.ReadUInt32(file);
            uint storedSize = (flags & 1) != 0 ? packedSize : unpackedSize;

            EnsurePlacement(file.Length, payloadOffset, storedSize);
            entries.Add(new ArcEntry(name, payloadOffset, storedSize, unpackedSize, flags));

            dataOffset = checked(payloadOffset + storedSize);
        }

        return new ArcArchive(path, ArcKind.Af01, entries);
    }

    private static ArcArchive ReadWfl(FileStream file, string path)
    {
        string ariPath = System.IO.Path.ChangeExtension(path, ".ari");
        if (File.Exists(ariPath))
            return ReadWflAri(file, path, ariPath);

        List<ArcEntry> entries = new();
        file.Position = 4;

        while (file.Position + 4 < file.Length)
        {
            int nameLength = BinaryStream.ReadInt32(file);
            byte[] encryptedName = ArcNameCodec.ReadName(file, nameLength);
            string name = ArcNameCodec.DecodeName(encryptedName);
            ushort mode = BinaryStream.ReadUInt16(file);
            uint storedSize = BinaryStream.ReadUInt32(file);
            uint unpackedSize = storedSize;
            if (mode == 1)
                unpackedSize = BinaryStream.ReadUInt32(file);

            long payloadOffset = file.Position;
            EnsurePlacement(file.Length, payloadOffset, storedSize);
            entries.Add(new ArcEntry(name, payloadOffset, storedSize, unpackedSize, mode));
            file.Position = checked(payloadOffset + storedSize);
        }

        return new ArcArchive(path, ArcKind.Wfl1, entries);
    }

    private static ArcArchive ReadWflAri(FileStream file, string path, string ariPath)
    {
        List<ArcEntry> entries = new();
        using FileStream ari = File.OpenRead(ariPath);
        long arcOffset = 4;

        while (ari.Position + 4 < ari.Length)
        {
            int nameLength = BinaryStream.ReadInt32(ari);
            byte[] encryptedName = ArcNameCodec.ReadName(ari, nameLength);
            string name = ArcNameCodec.DecodeName(encryptedName);
            ushort mode = BinaryStream.ReadUInt16(ari);
            uint storedSize = BinaryStream.ReadUInt32(ari);

            long payloadOffset = checked(arcOffset + nameLength + 10);
            uint unpackedSize = storedSize;
            if (mode == 1)
            {
                unpackedSize = BinaryStream.ReadUInt32At(file, payloadOffset);
                payloadOffset = checked(payloadOffset + 4);
            }

            EnsurePlacement(file.Length, payloadOffset, storedSize);
            entries.Add(new ArcEntry(name, payloadOffset, storedSize, unpackedSize, mode));
            arcOffset = checked(payloadOffset + storedSize);
        }

        return new ArcArchive(path, ArcKind.Wfl1, entries);
    }

    private static PackSummary WriteUf(FileStream output, IReadOnlyList<InputFile> files)
    {
        output.Write("UF01"u8);
        BinaryStream.WriteUInt32(output, 0);

        PackSummary summary = new();
        List<EntryIndex> index = new(files.Count);
        foreach (InputFile file in files)
        {
            PackedPayload payload = PreparePayload(file);
            byte[] encryptedName = ArcNameCodec.EncodeName(file.ArcName);
            BinaryStream.WriteUInt32(output, checked((uint)encryptedName.Length));
            output.Write(encryptedName);
            BinaryStream.WriteUInt16(output, payload.Flags);
            BinaryStream.WriteUInt32(output, payload.StoredSize);
            if (payload.IsPacked)
                BinaryStream.WriteUInt32(output, payload.UnpackedSize);
            WritePayload(output, file, payload);

            index.Add(new EntryIndex(encryptedName, payload.Flags, payload.StoredSize, payload.UnpackedSize));
            summary.Add(payload.UnpackedSize, payload.StoredSize, payload.IsPacked);
        }

        long indexStart = output.Position;
        foreach (EntryIndex entry in index)
        {
            BinaryStream.WriteUInt32(output, checked((uint)entry.EncryptedName.Length));
            output.Write(entry.EncryptedName);
            BinaryStream.WriteUInt16(output, entry.Flags);
            BinaryStream.WriteUInt32(output, entry.PackedSize);
        }

        BinaryStream.WriteUInt32At(output, 4, checked((uint)(indexStart - 4)));
        return summary;
    }

    private static PackSummary WriteAf(FileStream output, IReadOnlyList<InputFile> files)
    {
        output.Write("AF01"u8);
        BinaryStream.WriteUInt32(output, 1);
        BinaryStream.WriteUInt32(output, 0);

        PackSummary summary = new();
        List<EntryIndex> index = new(files.Count);
        foreach (InputFile file in files)
        {
            PackedPayload payload = PreparePayload(file);
            byte[] encryptedName = ArcNameCodec.EncodeName(file.ArcName);
            BinaryStream.WriteUInt32(output, checked((uint)encryptedName.Length));
            output.Write(encryptedName);
            BinaryStream.WriteUInt16(output, payload.Flags);
            BinaryStream.WriteUInt32(output, payload.StoredSize);
            if (payload.IsPacked)
                BinaryStream.WriteUInt32(output, payload.UnpackedSize);
            WritePayload(output, file, payload);

            index.Add(new EntryIndex(encryptedName, payload.Flags, payload.StoredSize, payload.UnpackedSize));
            summary.Add(payload.UnpackedSize, payload.StoredSize, payload.IsPacked);
        }

        long indexStart = output.Position;
        foreach (EntryIndex entry in index)
        {
            BinaryStream.WriteUInt32(output, checked((uint)entry.EncryptedName.Length));
            output.Write(entry.EncryptedName);
            BinaryStream.WriteUInt16(output, entry.Flags);
            BinaryStream.WriteUInt32(output, entry.PackedSize);
            BinaryStream.WriteUInt32(output, entry.UnpackedSize);
        }

        BinaryStream.WriteUInt32At(output, 8, checked((uint)(indexStart - 8)));
        return summary;
    }

    private static PackSummary WriteWfl(FileStream output, IReadOnlyList<InputFile> files, string archivePath)
    {
        output.Write("WFL1"u8);

        string ariPath = System.IO.Path.ChangeExtension(System.IO.Path.GetFullPath(archivePath), ".ari");
        string? ariParent = System.IO.Path.GetDirectoryName(ariPath);
        if (!string.IsNullOrEmpty(ariParent))
            Directory.CreateDirectory(ariParent);

        using FileStream ari = File.Create(ariPath);
        PackSummary summary = new();
        foreach (InputFile file in files)
        {
            PackedPayload payload = PreparePayload(file);
            ushort mode = GetWflMode(file, payload);
            byte[] encryptedName = ArcNameCodec.EncodeName(file.ArcName);

            WriteWflHeader(output, encryptedName, mode, payload, includeUnpackedSize: true);
            WriteWflHeader(ari, encryptedName, mode, payload, includeUnpackedSize: false);
            WritePayload(output, file, payload);

            summary.Add(payload.UnpackedSize, payload.StoredSize, payload.IsPacked);
        }

        return summary;
    }

    private static void WriteWflHeader(Stream output, byte[] encryptedName, ushort mode, PackedPayload payload, bool includeUnpackedSize)
    {
        BinaryStream.WriteUInt32(output, checked((uint)encryptedName.Length));
        output.Write(encryptedName);
        BinaryStream.WriteUInt16(output, mode);
        BinaryStream.WriteUInt32(output, payload.StoredSize);
        if (includeUnpackedSize && mode == 1)
            BinaryStream.WriteUInt32(output, payload.UnpackedSize);
    }

    private static ushort GetWflMode(InputFile file, PackedPayload payload)
    {
        if (payload.IsPacked)
            return 1;

        string extension = System.IO.Path.GetExtension(file.ArcName);
        return string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
            ? (ushort)2
            : (ushort)0;
    }

    private static PackedPayload PreparePayload(InputFile file)
    {
        if (!ArchiveCompressionPolicy.ShouldCompress(file))
            return PackedPayload.Plain(file.Size);

        byte[] raw = File.ReadAllBytes(file.FilePath);
        byte[] packed = KaguyaLz.Pack(raw);
        if (packed.Length + 4 < raw.Length)
            return PackedPayload.Packed(packed, file.Size);

        return PackedPayload.Plain(file.Size);
    }

    private static void WritePayload(Stream output, InputFile file, PackedPayload payload)
    {
        if (payload.Data is not null)
        {
            output.Write(payload.Data);
            return;
        }

        using FileStream input = File.OpenRead(file.FilePath);
        input.CopyTo(output);
    }

    private static void EnsurePlacement(long archiveLength, long offset, uint size)
    {
        if (offset < 0 || offset > archiveLength || checked(offset + (long)size) > archiveLength)
            throw new InvalidDataException($"entry points outside archive: offset=0x{offset:X}, size={size}");
    }

    private sealed record EntryIndex(byte[] EncryptedName, ushort Flags, uint PackedSize, uint UnpackedSize);

    private sealed record PackedPayload(byte[]? Data, uint StoredSize, uint UnpackedSize, bool IsPacked)
    {
        public ushort Flags => IsPacked ? (ushort)1 : (ushort)0;

        public static PackedPayload Plain(uint size)
        {
            return new PackedPayload(null, size, size, IsPacked: false);
        }

        public static PackedPayload Packed(byte[] data, uint unpackedSize)
        {
            return new PackedPayload(data, checked((uint)data.Length), unpackedSize, IsPacked: true);
        }
    }
}
