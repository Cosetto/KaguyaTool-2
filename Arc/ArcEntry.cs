namespace KaguyaArcTool.Arc;

internal sealed record ArcEntry(
    string Name,
    long Offset,
    uint StoredSize,
    uint UnpackedSize,
    ushort Flags)
{
    public bool IsPacked => (Flags & 1) != 0;
}
