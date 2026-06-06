namespace KaguyaArcTool.Arc;

internal sealed class PackSummary
{
    public int FileCount { get; private set; }
    public int CompressedCount { get; private set; }
    public ulong UnpackedBytes { get; private set; }
    public ulong StoredBytes { get; private set; }

    public int PlainCount => FileCount - CompressedCount;

    public void Add(uint unpackedSize, uint storedSize, bool isPacked)
    {
        FileCount++;
        if (isPacked)
            CompressedCount++;
        UnpackedBytes += unpackedSize;
        StoredBytes += storedSize;
    }
}
