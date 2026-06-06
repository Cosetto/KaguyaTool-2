using KaguyaArcTool.Arc;

namespace KaguyaArcTool.Commands;

internal static class ArchiveInfoPrinter
{
    public static void Print(ArcArchive arc, long archiveLength)
    {
        int packedCount = arc.Entries.Count(entry => entry.IsPacked);
        ulong unpackedBytes = 0;
        ulong storedBytes = 0;
        foreach (ArcEntry entry in arc.Entries)
        {
            unpackedBytes += entry.UnpackedSize;
            storedBytes += entry.StoredSize;
        }

        Console.WriteLine($"archive: {Path.GetFullPath(arc.Path)}");
        Console.WriteLine($"format: {arc.Kind.Signature()}");
        Console.WriteLine($"size: {archiveLength} byte(s)");
        Console.WriteLine($"entries: {arc.Entries.Count} ({packedCount} packed, {arc.Entries.Count - packedCount} plain)");
        Console.WriteLine($"stored bytes: {storedBytes}");
        Console.WriteLine($"unpacked bytes: {unpackedBytes}");
    }
}
