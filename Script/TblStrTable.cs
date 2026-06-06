using System.Buffers.Binary;
using System.Text;

namespace KaguyaArcTool.Script;

internal sealed class TblStrTable
{
    private const int UfMagic = 0x31304655;
    private const int AriHeaderAdjustment = 1;
    private readonly TblStrStorage _storage;
    private readonly List<int>? _logicalIndexes;

    private TblStrTable(List<TblStrEntry> entries, TblStrStorage storage, List<int>? logicalIndexes = null)
    {
        Entries = entries;
        _storage = storage;
        _logicalIndexes = logicalIndexes;
    }

    public List<TblStrEntry> Entries { get; }
    public IReadOnlyList<int>? LogicalIndexes => _logicalIndexes;

    public TblStrTable WithEntries(List<TblStrEntry> entries)
    {
        return new TblStrTable(entries, _storage, _logicalIndexes);
    }

    public static TblStrTable Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 8)
            throw new InvalidDataException("TBLSTR table is too small");

        int magic = ReadInt32(data, 0);
        if (magic == UfMagic)
            return LoadUf(data);

        string ariPath = Path.ChangeExtension(path, ".ari");
        if (File.Exists(ariPath))
            return LoadAri(data, File.ReadAllBytes(ariPath));

        throw new InvalidDataException("Unsupported TBLSTR");
    }

    private static TblStrTable LoadUf(byte[] data)
    {
        int indexOffset = ReadInt32(data, 4);
        if (indexOffset < 8 || indexOffset > data.Length || ((data.Length - indexOffset) & 3) != 0)
            throw new InvalidDataException("invalid TBLSTR index offset");

        int count = (data.Length - indexOffset) / 4;
        List<TblStrEntry> entries = new(count);
        Encoding encoding = Encoding.GetEncoding(932);

        for (int i = 0; i < count; i++)
        {
            int entryOffset = ReadInt32(data, indexOffset + i * 4);
            if (entryOffset < 8 || entryOffset > indexOffset - 8)
                throw new InvalidDataException($"invalid TBLSTR entry offset at index {i}");

            int type = ReadInt32(data, entryOffset);
            int length = ReadInt32(data, entryOffset + 4);
            int textOffset = entryOffset + 8;
            if (length < 0 || textOffset + length > data.Length)
                throw new InvalidDataException($"invalid TBLSTR string length at index {i}");

            byte[] bytes = new byte[length];
            Buffer.BlockCopy(data, textOffset, bytes, 0, length);
            Invert(bytes);
            string text = length == 0 ? string.Empty : encoding.GetString(bytes);
            entries.Add(new TblStrEntry(type, text, OriginalText: text));
        }

        return new TblStrTable(entries, TblStrStorage.Uf01);
    }

    private static TblStrTable LoadAri(byte[] arcData, byte[] ariData)
    {
        if ((ariData.Length & 3) != 0)
            throw new InvalidDataException("invalid TBLSTR ARI length");

        int storedLength = ReadInt32(arcData, 0);
        if (storedLength != arcData.Length)
            throw new InvalidDataException("invalid TBLSTR ARC length header");

        HashSet<int> indexedOffsets = new();
        List<int> ariOffsets = new();
        for (int pos = 4; pos < ariData.Length; pos += 4)
        {
            int ariOffset = ReadInt32(ariData, pos);
            indexedOffsets.Add(ariOffset);
            ariOffsets.Add(ariOffset);
        }

        List<TblStrEntry> entries = new();
        Dictionary<int, int> offsetToEntryIndex = new();
        Encoding encoding = Encoding.GetEncoding(932);
        int offset = 4;
        while (offset < arcData.Length)
        {
            if (offset + 8 > arcData.Length)
                throw new InvalidDataException($"truncated TBLSTR record at 0x{offset:X}");

            int type = ReadInt32(arcData, offset);
            int storedSize = ReadInt32(arcData, offset + 4);
            int textOffset = offset + 8;
            if (storedSize < 0 || textOffset + storedSize > arcData.Length)
                throw new InvalidDataException($"invalid TBLSTR record length at 0x{offset:X}");

            ReadOnlySpan<byte> raw = arcData.AsSpan(textOffset, storedSize);
            int terminator = raw.IndexOf((byte)0);
            if (terminator >= 0)
                raw = raw[..terminator];

            byte[] bytes = raw.ToArray();
            Invert(bytes);
            string text = bytes.Length == 0 ? string.Empty : encoding.GetString(bytes);
            offsetToEntryIndex.Add(offset, entries.Count);
            entries.Add(new TblStrEntry(type, text, indexedOffsets.Contains(offset), storedSize, text));

            offset = checked(textOffset + storedSize);
        }

        List<int> logicalIndexes = new(ariOffsets.Count);
        foreach (int ariOffset in ariOffsets)
        {
            if (!offsetToEntryIndex.TryGetValue(ariOffset, out int entryIndex))
                throw new InvalidDataException($"TBLSTR ARI offset does not point to a record: 0x{ariOffset:X}");

            logicalIndexes.Add(entryIndex);
        }

        return new TblStrTable(entries, TblStrStorage.Ari, logicalIndexes);
    }

    public void Save(string path)
    {
        if (_storage == TblStrStorage.Ari)
        {
            SaveAri(path);
            return;
        }

        SaveUf(path);
    }

    private void SaveUf(string path)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        Encoding encoding = Encoding.GetEncoding(932);
        using FileStream output = File.Create(path);

        WriteInt32(output, UfMagic);
        WriteInt32(output, 0);

        int[] offsets = new int[Entries.Count];
        for (int i = 0; i < Entries.Count; i++)
        {
            TblStrEntry entry = Entries[i];
            if (output.Position > int.MaxValue)
                throw new InvalidOperationException("TBLSTR table is too large");

            offsets[i] = checked((int)output.Position);
            byte[] bytes = encoding.GetBytes(entry.Text);
            Invert(bytes);

            WriteInt32(output, entry.Type);
            WriteInt32(output, bytes.Length);
            output.Write(bytes);
        }

        if (output.Position > int.MaxValue)
            throw new InvalidOperationException("TBLSTR table is too large");

        int indexOffset = checked((int)output.Position);
        foreach (int offset in offsets)
            WriteInt32(output, offset);

        output.Position = 4;
        WriteInt32(output, indexOffset);
    }

    private void SaveAri(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        string ariPath = Path.ChangeExtension(fullPath, ".ari");
        Encoding encoding = Encoding.GetEncoding(932);
        List<int> indexedOffsets = _logicalIndexes is null
            ? new List<int>()
            : Enumerable.Repeat(-1, _logicalIndexes.Count).ToList();
        Dictionary<int, List<int>> logicalSlots = BuildLogicalSlots();
        Dictionary<int, int> nameToMessage = BuildNameToMessageMap();

        using FileStream output = File.Create(fullPath);
        WriteInt32(output, 0);

        TblStrEntry? previousEntry = null;
        List<PendingAriOverflow> overflowRecords = new();
        HashSet<int> forcedOverflowEntries = new();
        Dictionary<int, byte[]> overflowNames = new();
        for (int entryIndex = 0; entryIndex < Entries.Count; entryIndex++)
        {
            TblStrEntry entry = Entries[entryIndex];
            if (output.Position > int.MaxValue)
                throw new InvalidOperationException("TBLSTR table is too large");

            int offset = checked((int)output.Position);
            List<int>? slots = logicalSlots.GetValueOrDefault(entryIndex);
            if (entry.IsIndexed)
            {
                if (slots is null || slots.Count == 0)
                    indexedOffsets.Add(offset);
                else
                {
                    foreach (int slot in slots)
                        indexedOffsets[slot] = offset;
                }
            }

            byte[] bytes = encoding.GetBytes(entry.Text);
            Invert(bytes);

            int storedSize = entry.StoredSize;
            if (storedSize < 0)
                storedSize = GetAriStoredSize(bytes.Length);

            if (bytes.Length + 1 > storedSize)
            {
                if (!entry.IsIndexed)
                {
                    if (TryMoveNameWithMessage(entryIndex, bytes, nameToMessage, forcedOverflowEntries, overflowNames))
                    {
                        bytes = encoding.GetBytes(entry.OriginalText ?? string.Empty);
                        Invert(bytes);
                        if (bytes.Length + 1 > storedSize)
                            bytes = [];
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"TBLSTR direct-offset text is too long for its fixed Jyuku record slot. " +
                            $"type={entry.Type}, encoded={bytes.Length} byte(s), capacity={storedSize - 1} byte(s), text={entry.Text}");
                    }
                }
                else
                {
                    QueueOverflow(entryIndex, entry, bytes, slots, overflowRecords, overflowNames, previousEntry, encoding);

                    bytes = encoding.GetBytes(entry.OriginalText ?? string.Empty);
                    Invert(bytes);
                    if (bytes.Length + 1 > storedSize)
                        bytes = [];
                }
            }
            else if (forcedOverflowEntries.Contains(entryIndex))
            {
                QueueOverflow(entryIndex, entry, bytes, slots, overflowRecords, overflowNames, previousEntry, encoding);
                bytes = encoding.GetBytes(entry.OriginalText ?? string.Empty);
                Invert(bytes);
                if (bytes.Length + 1 > storedSize)
                    bytes = [];
            }

            WriteInt32(output, entry.Type);
            WriteInt32(output, storedSize);
            WriteAriPayload(output, bytes, storedSize);
            previousEntry = entry;
        }

        foreach (PendingAriOverflow overflow in overflowRecords)
        {
            if (output.Position > int.MaxValue)
                throw new InvalidOperationException("TBLSTR table is too large");

            if (overflow.EncryptedName is not null)
            {
                int nameStoredSize = GetAriStoredSize(overflow.EncryptedName.Length);
                WriteInt32(output, 2);
                WriteInt32(output, nameStoredSize);
                WriteAriPayload(output, overflow.EncryptedName, nameStoredSize);
            }

            indexedOffsets[overflow.IndexSlot] = checked((int)output.Position);
            int storedSize = GetAriStoredSize(overflow.EncryptedText.Length);
            WriteInt32(output, overflow.Type);
            WriteInt32(output, storedSize);
            WriteAriPayload(output, overflow.EncryptedText, storedSize);
        }

        if (output.Position > int.MaxValue)
            throw new InvalidOperationException("TBLSTR table is too large");

        int length = checked((int)output.Position);
        output.Position = 0;
        WriteInt32(output, length);

        using FileStream ari = File.Create(ariPath);
        if (indexedOffsets.Any(offset => offset < 0))
            throw new InvalidDataException("TBLSTR logical ARI order was not fully written");

        WriteInt32(ari, checked(indexedOffsets.Count + AriHeaderAdjustment));
        foreach (int offset in indexedOffsets)
            WriteInt32(ari, offset);
    }

    private Dictionary<int, List<int>> BuildLogicalSlots()
    {
        Dictionary<int, List<int>> slots = new();
        if (_logicalIndexes is null)
            return slots;

        for (int i = 0; i < _logicalIndexes.Count; i++)
        {
            int entryIndex = _logicalIndexes[i];
            if (!slots.TryGetValue(entryIndex, out List<int>? entrySlots))
            {
                entrySlots = new List<int>();
                slots.Add(entryIndex, entrySlots);
            }

            entrySlots.Add(i);
        }

        return slots;
    }

    private bool TryMoveNameWithMessage(
        int entryIndex,
        byte[] encryptedName,
        Dictionary<int, int> nameToMessage,
        HashSet<int> forcedOverflowEntries,
        Dictionary<int, byte[]> overflowNames)
    {
        if (Entries[entryIndex].Type != 2)
            return false;

        if (!nameToMessage.TryGetValue(entryIndex, out int messageIndex))
            return false;

        TblStrEntry message = Entries[messageIndex];
        if (!message.IsIndexed || message.Type != 0)
            return false;

        forcedOverflowEntries.Add(messageIndex);
        overflowNames[messageIndex] = encryptedName;
        return true;
    }

    private Dictionary<int, int> BuildNameToMessageMap()
    {
        Dictionary<int, int> nameToMessage = new();
        if (_logicalIndexes is null)
            return nameToMessage;

        int logicalCursor = 0;
        for (int physicalMessageIndex = 0; physicalMessageIndex < Entries.Count; physicalMessageIndex++)
        {
            if (Entries[physicalMessageIndex].Type != 0)
                continue;

            if (logicalCursor >= _logicalIndexes.Count)
                break;

            int logicalMessageIndex = _logicalIndexes[logicalCursor++];
            int nameIndex = FindAriNameIndex(physicalMessageIndex, logicalMessageIndex);
            if (nameIndex >= 0)
                nameToMessage[nameIndex] = logicalMessageIndex;
        }

        return nameToMessage;
    }

    private int FindAriNameIndex(int physicalMessageIndex, int logicalMessageIndex)
    {
        if (physicalMessageIndex > 0 && Entries[physicalMessageIndex - 1].Type == 2)
            return physicalMessageIndex - 1;

        if (logicalMessageIndex > 0 && Entries[logicalMessageIndex - 1].Type == 2)
            return logicalMessageIndex - 1;

        return -1;
    }

    private static void QueueOverflow(
        int entryIndex,
        TblStrEntry entry,
        byte[] encryptedText,
        List<int>? slots,
        List<PendingAriOverflow> overflowRecords,
        Dictionary<int, byte[]> overflowNames,
        TblStrEntry? previousEntry,
        Encoding encoding)
    {
        if (slots is null || slots.Count == 0)
            throw new InvalidDataException("TBLSTR indexed record is missing from logical ARI order");

        byte[]? nameBytes = overflowNames.GetValueOrDefault(entryIndex);
        if (nameBytes is null && previousEntry?.Type == 2)
        {
            nameBytes = encoding.GetBytes(previousEntry.Text);
            Invert(nameBytes);
        }

        foreach (int slot in slots)
            overflowRecords.Add(new PendingAriOverflow(slot, entry.Type, encryptedText, nameBytes));
    }

    private static int GetAriStoredSize(int textSize)
    {
        int sizeWithTerminator = checked(textSize + 1);
        return (sizeWithTerminator + 3) & ~3;
    }

    private static void WriteAriPayload(Stream output, byte[] encryptedText, int storedSize)
    {
        output.Write(encryptedText);
        output.WriteByte(0);
        for (int i = encryptedText.Length + 1; i < storedSize; i++)
            output.WriteByte(0);
    }

    private static int ReadInt32(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
    }

    private static void WriteInt32(Stream output, int value)
    {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        output.Write(data);
    }

    private static void Invert(byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)~bytes[i];
    }

    private enum TblStrStorage
    {
        Uf01,
        Ari
    }

    private sealed record PendingAriOverflow(int IndexSlot, int Type, byte[] EncryptedText, byte[]? EncryptedName);
}
